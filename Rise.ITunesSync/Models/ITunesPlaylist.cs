using System.Collections.Generic;

namespace Rise.ITunesSync.Models
{
    /// <summary>Represents a playlist parsed from iTunes Music Library.xml.</summary>
    public sealed class ITunesPlaylist
    {
        public string       PlaylistId { get; set; } = string.Empty;
        public string       Name       { get; set; } = string.Empty;
        public List<string> TrackIds   { get; set; } = new();
    }
}
