using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rise.App.ViewModels;

namespace Rise.App.UserControls
{
    public sealed partial class SongTableRow : UserControl
    {
        // ── Song data ─────────────────────────────────────────────────────────

        public static readonly DependencyProperty SongProperty =
            DependencyProperty.Register(nameof(Song), typeof(SongViewModel),
                typeof(SongTableRow), new PropertyMetadata(null));
        public SongViewModel Song
        {
            get => (SongViewModel)GetValue(SongProperty);
            set => SetValue(SongProperty, value);
        }

        // ── Column visibility ─────────────────────────────────────────────────

        private static DependencyProperty ColProp(string name) =>
            DependencyProperty.Register(name, typeof(bool),
                typeof(SongTableRow), new PropertyMetadata(true));

        public static readonly DependencyProperty ShowAlbumProperty       = ColProp(nameof(ShowAlbum));
        public static readonly DependencyProperty ShowArtistProperty      = ColProp(nameof(ShowArtist));
        public static readonly DependencyProperty ShowAlbumArtistProperty = ColProp(nameof(ShowAlbumArtist));
        public static readonly DependencyProperty ShowPlayCountProperty   = ColProp(nameof(ShowPlayCount));
        public static readonly DependencyProperty ShowSkipCountProperty   = ColProp(nameof(ShowSkipCount));
        public static readonly DependencyProperty ShowRatingProperty      = ColProp(nameof(ShowRating));
        public static readonly DependencyProperty ShowYearProperty        = ColProp(nameof(ShowYear));
        public static readonly DependencyProperty ShowDurationProperty    = ColProp(nameof(ShowDuration));
        public static readonly DependencyProperty ShowDateAddedProperty   = ColProp(nameof(ShowDateAdded));
        public static readonly DependencyProperty ShowLastPlayedProperty  = ColProp(nameof(ShowLastPlayed));
        public static readonly DependencyProperty ShowBitrateProperty     = ColProp(nameof(ShowBitrate));
        public static readonly DependencyProperty ShowInlineArtistProperty =
            DependencyProperty.Register(nameof(ShowInlineArtist), typeof(bool),
                typeof(SongTableRow), new PropertyMetadata(false));

        public bool ShowAlbum        { get => (bool)GetValue(ShowAlbumProperty);        set => SetValue(ShowAlbumProperty, value); }
        public bool ShowArtist       { get => (bool)GetValue(ShowArtistProperty);       set => SetValue(ShowArtistProperty, value); }
        public bool ShowAlbumArtist  { get => (bool)GetValue(ShowAlbumArtistProperty);  set => SetValue(ShowAlbumArtistProperty, value); }
        public bool ShowPlayCount    { get => (bool)GetValue(ShowPlayCountProperty);    set => SetValue(ShowPlayCountProperty, value); }
        public bool ShowSkipCount    { get => (bool)GetValue(ShowSkipCountProperty);    set => SetValue(ShowSkipCountProperty, value); }
        public bool ShowRating       { get => (bool)GetValue(ShowRatingProperty);       set => SetValue(ShowRatingProperty, value); }
        public bool ShowYear         { get => (bool)GetValue(ShowYearProperty);         set => SetValue(ShowYearProperty, value); }
        public bool ShowDuration     { get => (bool)GetValue(ShowDurationProperty);     set => SetValue(ShowDurationProperty, value); }
        public bool ShowDateAdded    { get => (bool)GetValue(ShowDateAddedProperty);    set => SetValue(ShowDateAddedProperty, value); }
        public bool ShowLastPlayed   { get => (bool)GetValue(ShowLastPlayedProperty);   set => SetValue(ShowLastPlayedProperty, value); }
        public bool ShowBitrate      { get => (bool)GetValue(ShowBitrateProperty);      set => SetValue(ShowBitrateProperty, value); }
        public bool ShowInlineArtist { get => (bool)GetValue(ShowInlineArtistProperty); set => SetValue(ShowInlineArtistProperty, value); }

        // ── Playback state ────────────────────────────────────────────────────

        public static readonly DependencyProperty IsPlayingProperty =
            DependencyProperty.Register(nameof(IsPlaying), typeof(bool),
                typeof(SongTableRow), new PropertyMetadata(false));
        public bool IsPlaying
        {
            get => (bool)GetValue(IsPlayingProperty);
            set => SetValue(IsPlayingProperty, value);
        }

        public static readonly DependencyProperty TrackNumberProperty =
            DependencyProperty.Register(nameof(TrackNumber), typeof(string),
                typeof(SongTableRow), new PropertyMetadata(string.Empty));
        public string TrackNumber
        {
            get => (string)GetValue(TrackNumberProperty);
            set => SetValue(TrackNumberProperty, value);
        }

        public static readonly DependencyProperty MaxPlayCountProperty =
            DependencyProperty.Register(nameof(MaxPlayCount), typeof(int),
                typeof(SongTableRow), new PropertyMetadata(1));
        public int MaxPlayCount
        {
            get => (int)GetValue(MaxPlayCountProperty);
            set => SetValue(MaxPlayCountProperty, value);
        }

        // ── Commands ──────────────────────────────────────────────────────────

        public static readonly DependencyProperty PlayCommandProperty =
            DependencyProperty.Register(nameof(PlayCommand), typeof(IRelayCommand),
                typeof(SongTableRow), new PropertyMetadata(null));
        public IRelayCommand PlayCommand
        {
            get => (IRelayCommand)GetValue(PlayCommandProperty);
            set => SetValue(PlayCommandProperty, value);
        }

        public SongTableRow()
        {
            InitializeComponent();
        }
    }
}
