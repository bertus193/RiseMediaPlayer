using Rise.ITunesSync.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Rise.ITunesSync.Services
{
    /// <summary>
    /// Parses iTunes Music Library.xml (Apple plist format) into
    /// <see cref="ITunesTrack"/> and <see cref="ITunesPlaylist"/> objects.
    ///
    /// Adapted from AlberItunesSync.Services.ITunesLibraryParser.
    /// Removed: backup/restore logic, SaveLibrary, UpdateTrack —
    /// Rise is the new source of truth so we only READ iTunes data.
    /// </summary>
    public sealed class ITunesLibraryParser
    {
        public IReadOnlyList<ITunesTrack>    Tracks    { get; private set; } = Array.Empty<ITunesTrack>();
        public IReadOnlyList<ITunesPlaylist> Playlists { get; private set; } = Array.Empty<ITunesPlaylist>();
        public string                        LibraryPath { get; private set; } = string.Empty;

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Loads and parses the library at <paramref name="libraryPath"/>.
        /// Throws <see cref="FileNotFoundException"/> if the file does not exist.
        /// </summary>
        public void LoadLibrary(string libraryPath)
        {
            if (!File.Exists(libraryPath))
                throw new FileNotFoundException("iTunes library file not found.", libraryPath);

            LibraryPath = libraryPath;
            var doc = XDocument.Load(libraryPath);

            Tracks    = ParseTracks(doc);
            Playlists = ParsePlaylists(doc);
        }

        // ── Auto-detect helpers ────────────────────────────────────────────────

        /// <summary>
        /// Returns the default iTunes library XML path for the current user.
        /// Returns null if the file is not found.
        /// </summary>
        public static string? TryFindDefaultLibraryPath()
        {
            // Standard location on Windows
            string musicFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            var candidates = new[]
            {
                Path.Combine(musicFolder, "iTunes", "iTunes Music Library.xml"),
                Path.Combine(musicFolder, "iTunes", "iTunes Library.xml"),
                // Older iTunes versions
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Music", "iTunes", "iTunes Music Library.xml"),
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        // ── Parsing ────────────────────────────────────────────────────────────

        private static List<ITunesTrack> ParseTracks(XDocument doc)
        {
            var result = new List<ITunesTrack>();

            var tracksDict = doc.Root?
                .Element("dict")?
                .Elements()
                .SkipWhile(e => e.Name != "key" || e.Value != "Tracks")
                .Skip(1)
                .FirstOrDefault()?
                .Elements("dict");

            if (tracksDict == null) return result;

            foreach (var dict in tracksDict)
            {
                var track = ParseTrackDict(dict);
                if (track != null) result.Add(track);
            }

            return result;
        }

        private static ITunesTrack? ParseTrackDict(XElement dict)
        {
            var track = new ITunesTrack();
            var elems = dict.Elements().ToList();

            for (int i = 0; i + 1 < elems.Count; i += 2)
            {
                if (elems[i].Name != "key") continue;
                string key = elems[i].Value;
                string val = elems[i + 1].Value;

                switch (key)
                {
                    case "Track ID":      track.TrackId      = val; break;
                    case "Persistent ID": track.PersistentId = val; break;
                    case "Name":          track.Name         = val; break;
                    case "Artist":        track.Artist       = val; break;
                    case "Album":         track.Album        = val; break;
                    case "Album Artist":  track.AlbumArtist  = val; break;
                    case "Genre":         track.Genre        = val; break;
                    case "Composer":      track.Composer     = val; break;
                    case "Comments":      track.Comment      = val; break;
                    case "Year":          track.Year         = ParseInt(val); break;
                    case "Track Number":  track.TrackNumber  = ParseInt(val); break;
                    case "Disc Number":   track.DiscNumber   = ParseInt(val); break;
                    case "Total Time":    track.TotalTime    = ParseInt(val); break;
                    case "Play Count":    track.PlayCount    = ParseInt(val); break;
                    case "Skip Count":    track.SkipCount    = ParseInt(val); break;
                    case "Rating":        track.Rating       = ParseInt(val); break;
                    case "Date Added":    track.DateAdded    = ParseDate(val); break;
                    case "Play Date UTC": track.PlayDate     = ParseDate(val); break;
                    case "Date Modified": track.DateModified = ParseDate(val); break;
                    case "Location":
                        // Decode file:// URI → local path
                        track.Location = Uri.UnescapeDataString(
                            val.Replace("file://localhost/", "")
                               .Replace("file:///", ""));
                        break;
                }
            }

            return string.IsNullOrEmpty(track.TrackId) ? null : track;
        }

        private static List<ITunesPlaylist> ParsePlaylists(XDocument doc)
        {
            var result = new List<ITunesPlaylist>();

            var arr = doc.Root?
                .Element("dict")?
                .Elements()
                .SkipWhile(e => e.Name != "key" || e.Value != "Playlists")
                .Skip(1)
                .FirstOrDefault()?
                .Elements("dict");

            if (arr == null) return result;

            foreach (var dict in arr)
            {
                var pl = ParsePlaylistDict(dict);
                if (pl != null && !pl.Name.StartsWith("####"))
                    result.Add(pl);
            }

            return result;
        }

        private static ITunesPlaylist? ParsePlaylistDict(XElement dict)
        {
            var pl = new ITunesPlaylist();
            var elems = dict.Elements().ToList();

            for (int i = 0; i + 1 < elems.Count; i += 2)
            {
                if (elems[i].Name != "key") continue;
                string key = elems[i].Value;
                var    valElem = elems[i + 1];

                switch (key)
                {
                    case "Playlist ID": pl.PlaylistId = valElem.Value; break;
                    case "Name":        pl.Name       = valElem.Value; break;
                    case "Playlist Items":
                        if (valElem.Name == "array")
                        {
                            foreach (var itemDict in valElem.Elements("dict"))
                            {
                                var idElem = itemDict.Elements()
                                    .SkipWhile(e => e.Name != "key" || e.Value != "Track ID")
                                    .Skip(1)
                                    .FirstOrDefault();
                                if (idElem != null)
                                    pl.TrackIds.Add(idElem.Value);
                            }
                        }
                        break;
                }
            }

            return string.IsNullOrEmpty(pl.PlaylistId) ? null : pl;
        }

        // ── Type helpers ───────────────────────────────────────────────────────

        private static int ParseInt(string value)
            => int.TryParse(value, out int result) ? result : 0;

        private static DateTime? ParseDate(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;

            if (DateTime.TryParse(value, null,
                DateTimeStyles.RoundtripKind, out DateTime parsed))
            {
                if (parsed.Kind == DateTimeKind.Unspecified)
                    parsed = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
                return parsed.ToLocalTime();
            }

            return null;
        }
    }
}
