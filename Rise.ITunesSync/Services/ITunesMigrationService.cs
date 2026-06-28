using Rise.Common.Extensions;
using Rise.ITunesSync.Models;
using Rise.Models;
using Rise.NewRepository;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace Rise.ITunesSync.Services
{
    /// <summary>
    /// Progress snapshot reported during a migration run.
    /// </summary>
    public sealed class MigrationProgress
    {
        public string  Phase         { get; init; } = string.Empty;
        public int     Current       { get; init; }
        public int     Total         { get; init; }
        public double  Percentage    => Total == 0 ? 0 : (double)Current / Total * 100;
    }

    /// <summary>
    /// Summary returned when <see cref="ITunesMigrationService.RunAsync"/> completes.
    /// </summary>
    public sealed class MigrationResult
    {
        public int  SongsMatched       { get; init; }
        public int  SongsUnmatched     { get; init; }
        public int  RatingsImported    { get; init; }
        public int  PlayCountsImported { get; init; }
        public int  PlaylistsImported  { get; init; }
        public bool LibraryFolderAdded { get; init; }
        public string? Error           { get; init; }
    }

    /// <summary>
    /// One-time migration service: reads iTunes Music Library.xml
    /// and writes play counts, ratings, last-played dates and playlists
    /// into Rise's SQLite database and playlist JSON backend.
    ///
    /// Designed to be run once from the ITunesMigrationPage wizard.
    /// After the migration completes the user can uninstall iTunes.
    /// </summary>
    public sealed class ITunesMigrationService
    {
        private readonly ITunesLibraryParser _parser = new();

        // ── Options ────────────────────────────────────────────────────────────

        public bool ImportPlayCounts  { get; set; } = true;
        public bool ImportRatings     { get; set; } = true;
        public bool ImportPlaylists   { get; set; } = true;
        public bool AddLibraryFolder  { get; set; } = true;

        // ── Preview ────────────────────────────────────────────────────────────

        /// <summary>
        /// Loads the library and returns preview statistics without
        /// writing anything to the database.
        /// </summary>
        public MigrationPreview Preview(string libraryPath)
        {
            _parser.LoadLibrary(libraryPath);

            return new MigrationPreview
            {
                TotalTracks     = _parser.Tracks.Count,
                TracksWithRating    = _parser.Tracks.Count(t => t.Rating > 0),
                TracksWithPlayCount = _parser.Tracks.Count(t => t.PlayCount > 0),
                TracksWithPlayDate  = _parser.Tracks.Count(t => t.PlayDate.HasValue),
                TotalPlaylists      = _parser.Playlists.Count,
                LibraryFolderPath   = TryExtractMusicFolder(),
            };
        }

        // ── Migration ──────────────────────────────────────────────────────────

        /// <summary>
        /// Executes the full migration pipeline.
        /// </summary>
        public async Task<MigrationResult> RunAsync(
            string libraryPath,
            IProgress<MigrationProgress>? progress = null,
            CancellationToken ct = default)
        {
            try
            {
                // Phase 1 ─ Parse library XML
                Report(progress, "Leyendo biblioteca de iTunes…", 0, 1);
                _parser.LoadLibrary(libraryPath);
                ct.ThrowIfCancellationRequested();

                // Phase 2 ─ Add iTunes Media folder to Rise library
                bool folderAdded = false;
                if (AddLibraryFolder)
                {
                    Report(progress, "Añadiendo carpeta de música a Rise…", 0, 1);
                    folderAdded = await TryAddLibraryFolderAsync();
                    ct.ThrowIfCancellationRequested();
                }

                // Phase 3 ─ Match tracks and write stats
                Report(progress, "Emparejando canciones…", 0, _parser.Tracks.Count);

                var allSongs = await Repository.GetItemsAsync<Song>();
                // Build a lookup: normalised file path → Song
                var songIndex = allSongs
                    .Where(s => !string.IsNullOrEmpty(s.Location))
                    .ToDictionary(
                        s => NormalisePath(s.Location),
                        s => s,
                        StringComparer.OrdinalIgnoreCase);

                // Also index by filename only as a fallback
                var songByFileName = allSongs
                    .Where(s => !string.IsNullOrEmpty(s.Location))
                    .GroupBy(s => Path.GetFileName(s.Location), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                // TrackId → ITunesTrack for playlist resolution
                var trackById = _parser.Tracks
                    .ToDictionary(t => t.TrackId, StringComparer.OrdinalIgnoreCase);

                int matched = 0, unmatched = 0, ratings = 0, playcounts = 0;
                var toSave = new List<Song>();
                int total  = _parser.Tracks.Count;

                for (int i = 0; i < _parser.Tracks.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    if (i % 100 == 0)
                        Report(progress, "Importando estadísticas…", i, total);

                    var track = _parser.Tracks[i];
                    var song  = FindSong(track, songIndex, songByFileName);

                    if (song == null) { unmatched++; continue; }
                    matched++;

                    bool dirty = false;

                    if (ImportPlayCounts && track.PlayCount > 0)
                    {
                        song.PlayCount = Math.Max(song.PlayCount, track.PlayCount);
                        song.SkipCount = Math.Max(song.SkipCount, track.SkipCount);
                        if (track.PlayDate.HasValue &&
                            (!song.LastPlayed.HasValue || track.PlayDate > song.LastPlayed))
                            song.LastPlayed = track.PlayDate;
                        playcounts++;
                        dirty = true;
                    }

                    if (ImportRatings && track.Rating > 0)
                    {
                        song.Rating = track.ToRiseRating();
                        ratings++;
                        dirty = true;
                    }

                    if (dirty) toSave.Add(song);
                }

                // Bulk-save in batches of 200 to avoid long DB locks
                Report(progress, "Guardando en base de datos…", 0, toSave.Count);
                for (int i = 0; i < toSave.Count; i += 200)
                {
                    ct.ThrowIfCancellationRequested();
                    var batch = toSave.Skip(i).Take(200);
                    foreach (var s in batch)
                        await Repository.UpsertAsync(s);
                    Report(progress, "Guardando en base de datos…",
                        Math.Min(i + 200, toSave.Count), toSave.Count);
                }

                // Phase 4 ─ Import playlists
                int playlists = 0;
                if (ImportPlaylists && _parser.Playlists.Count > 0)
                {
                    Report(progress, "Importando playlists…", 0, _parser.Playlists.Count);
                    playlists = await ImportPlaylistsAsync(
                        _parser.Playlists, trackById, songIndex, songByFileName,
                        progress, ct);
                }

                Report(progress, "Migración completada", total, total);

                return new MigrationResult
                {
                    SongsMatched       = matched,
                    SongsUnmatched     = unmatched,
                    RatingsImported    = ratings,
                    PlayCountsImported = playcounts,
                    PlaylistsImported  = playlists,
                    LibraryFolderAdded = folderAdded,
                };
            }
            catch (OperationCanceledException)
            {
                return new MigrationResult { Error = "Cancelado por el usuario." };
            }
            catch (Exception ex)
            {
                return new MigrationResult { Error = ex.Message };
            }
        }

        // ── Playlist import ────────────────────────────────────────────────────

        private async Task<int> ImportPlaylistsAsync(
            IReadOnlyList<ITunesPlaylist> iTunesPlaylists,
            Dictionary<string, ITunesTrack> trackById,
            Dictionary<string, Song> songIndex,
            Dictionary<string, Song> songByFileName,
            IProgress<MigrationProgress>? progress,
            CancellationToken ct)
        {
            // Load existing Rise playlists to avoid duplicates
            var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var playlistsFolder = await ApplicationData.Current.LocalFolder
                .CreateFolderAsync("Playlists", CreationCollisionOption.OpenIfExists);

            foreach (var file in await playlistsFolder.GetFilesAsync())
                existingNames.Add(Path.GetFileNameWithoutExtension(file.Name));

            int imported = 0;
            for (int i = 0; i < iTunesPlaylists.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                Report(progress, "Importando playlists…", i, iTunesPlaylists.Count);

                var iTunesPl = iTunesPlaylists[i];
                if (existingNames.Contains(iTunesPl.Name)) continue;
                if (iTunesPl.TrackIds.Count == 0) continue;

                // Resolve track IDs to Song IDs
                var songIds = new List<Guid>();
                foreach (var tid in iTunesPl.TrackIds)
                {
                    if (!trackById.TryGetValue(tid, out var track)) continue;
                    var song = FindSong(track, songIndex, songByFileName);
                    if (song != null) songIds.Add(song.Id);
                }

                if (songIds.Count == 0) continue;

                // Persist as a JSON file — matches Rise.App's JsonBackendController<PlaylistViewModel> format
                var playlistJson = BuildPlaylistJson(iTunesPl.Name, songIds);
                var file = await playlistsFolder.CreateFileAsync(
                    $"{iTunesPl.Name.AsValidFileName()}.json",
                    CreationCollisionOption.FailIfExists);
                await FileIO.WriteTextAsync(file, playlistJson);
                imported++;
            }

            return imported;
        }

        private static string BuildPlaylistJson(string name, List<Guid> songIds)
        {
            // Minimal JSON that matches PlaylistViewModel serialisation
            var ids = string.Join(",\n    ",
                songIds.Select(id => $"\"{id}\""));
            return $$"""
{
  "Id": "{{Guid.NewGuid()}}",
  "Title": {{System.Text.Json.JsonSerializer.Serialize(name)}},
  "Icon": "\uE142",
  "Description": "Importada desde iTunes",
  "Songs": [
    {{ids}}
  ],
  "Videos": []
}
""";
        }

        // ── Track matching ─────────────────────────────────────────────────────

        private static Song? FindSong(
            ITunesTrack track,
            Dictionary<string, Song> byPath,
            Dictionary<string, Song> byFileName)
        {
            if (string.IsNullOrEmpty(track.Location)) return null;

            // Primary: exact path match
            if (byPath.TryGetValue(NormalisePath(track.Location), out var song))
                return song;

            // Fallback: filename only (covers relocated libraries)
            var fileName = Path.GetFileName(track.Location);
            if (!string.IsNullOrEmpty(fileName) &&
                byFileName.TryGetValue(fileName, out var byName))
                return byName;

            return null;
        }

        // ── Library folder ─────────────────────────────────────────────────────

        private async Task<bool> TryAddLibraryFolderAsync()
        {
            try
            {
                string? folder = TryExtractMusicFolder();
                if (folder == null || !Directory.Exists(folder)) return false;

                var sf = await StorageFolder.GetFolderFromPathAsync(folder);
                var lib = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Music);

                // Only add if not already present
                bool already = lib.Folders.Any(
                    f => string.Equals(f.Path, sf.Path, StringComparison.OrdinalIgnoreCase));

                if (!already)
                    await lib.RequestAddFolderAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        private string? TryExtractMusicFolder()
        {
            if (string.IsNullOrEmpty(_parser.LibraryPath)) return null;

            // iTunes stores music in a "iTunes Media" or "iTunes Music" subfolder
            // next to the library XML
            var dir = Path.GetDirectoryName(_parser.LibraryPath);
            if (dir == null) return null;

            var candidates = new[]
            {
                Path.Combine(dir, "iTunes Media"),
                Path.Combine(dir, "iTunes Music"),
                dir,
            };

            return candidates.FirstOrDefault(Directory.Exists);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static string NormalisePath(string path)
            => path.Replace('/', Path.DirectorySeparatorChar)
                   .TrimEnd(Path.DirectorySeparatorChar)
                   .ToLowerInvariant();

        private static void Report(
            IProgress<MigrationProgress>? progress,
            string phase, int current, int total)
            => progress?.Report(new MigrationProgress
            {
                Phase   = phase,
                Current = current,
                Total   = total,
            });
    }

    /// <summary>Preview data shown in the wizard before the user starts the migration.</summary>
    public sealed class MigrationPreview
    {
        public int     TotalTracks          { get; init; }
        public int     TracksWithRating     { get; init; }
        public int     TracksWithPlayCount  { get; init; }
        public int     TracksWithPlayDate   { get; init; }
        public int     TotalPlaylists       { get; init; }
        public string? LibraryFolderPath    { get; init; }
    }
}
