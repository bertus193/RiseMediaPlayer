using System;

namespace Rise.ITunesSync.Models
{
    /// <summary>
    /// Represents a single track entry parsed from iTunes Music Library.xml.
    /// Only fields relevant to the one-time migration into Rise are included.
    /// Android sync fields (AndroidPlayCount, AndroidRating, etc.) are omitted
    /// because Rise is the new source of truth after migration.
    /// </summary>
    public sealed class ITunesTrack
    {
        public string TrackId      { get; set; } = string.Empty;
        public string PersistentId { get; set; } = string.Empty;
        public string Name         { get; set; } = string.Empty;
        public string Artist       { get; set; } = string.Empty;
        public string Album        { get; set; } = string.Empty;
        public string AlbumArtist  { get; set; } = string.Empty;
        public string Genre        { get; set; } = string.Empty;
        public string Composer     { get; set; } = string.Empty;
        public string Comment      { get; set; } = string.Empty;
        public string Location     { get; set; } = string.Empty;

        public int  PlayCount   { get; set; }
        public int  SkipCount   { get; set; }
        public int  Rating      { get; set; }  // iTunes 0–100
        public int  Year        { get; set; }
        public int  TrackNumber { get; set; }
        public int  DiscNumber  { get; set; }
        public int  TotalTime   { get; set; }  // milliseconds

        public DateTime? DateAdded    { get; set; }
        public DateTime? PlayDate     { get; set; }
        public DateTime? DateModified { get; set; }

        /// <summary>
        /// Converts the iTunes 0–100 rating to Rise's 0–5 star scale.
        /// iTunes stores ratings in multiples of 20 (20=1★, 40=2★ … 100=5★).
        /// </summary>
        public uint ToRiseRating() => (uint)(Rating / 20);
    }
}
