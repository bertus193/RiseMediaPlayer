using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Rise.App.Dialogs;
using Rise.App.UserControls;
using Rise.App.ViewModels;
using Rise.Common.Extensions.Markup;
using Rise.Common.Helpers;
using Rise.Data.Collections;
using System;
using System.Linq;

namespace Rise.App.Views
{
    public sealed partial class SongsTablePage : MediaPageBase
    {
        private MainViewModel     MViewModel  => App.MViewModel;
        private SettingsViewModel SViewModel  => App.SViewModel;

        public SongViewModel SelectedItem
        {
            get => (SongViewModel)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        // ── Column visibility ─────────────────────────────────────────────────
        // Default set mirrors the columns iTunes shows by default.

        public bool ShowAlbum        { get; set; } = true;
        public bool ShowArtist       { get; set; } = true;
        public bool ShowAlbumArtist  { get; set; } = false;
        public bool ShowPlayCount    { get; set; } = true;
        public bool ShowSkipCount    { get; set; } = false;
        public bool ShowRating       { get; set; } = true;
        public bool ShowYear         { get; set; } = true;
        public bool ShowDuration     { get; set; } = true;
        public bool ShowDateAdded    { get; set; } = false;
        public bool ShowLastPlayed   { get; set; } = false;
        public bool ShowBitrate      { get; set; } = false;

        /// <summary>
        /// Shows artist as a secondary line inside Title when both Album
        /// and Artist columns are hidden — preserves scannability.
        /// </summary>
        public bool ShowInlineArtist => !ShowAlbum && !ShowArtist;

        // ── Play count scaling ────────────────────────────────────────────────

        public int MaxPlayCount { get; private set; } = 1;

        // ── Sort command (single RelayCommand, takes delegate key as param) ───

        [RelayCommand]
        private void Sort(string delegateKey)
        {
            // Toggle direction if same column, else reset to Ascending
            if (_currentSortKey == delegateKey)
            {
                _sortAscending = !_sortAscending;
                MediaViewModel.UpdateSortDirectionCommand.Execute(
                    _sortAscending ? SortDirection.Ascending : SortDirection.Descending);
            }
            else
            {
                _currentSortKey = delegateKey;
                _sortAscending  = true;
                bool alphabetical = delegateKey.StartsWith("G");
                CreateViewModel(delegateKey, SortDirection.Ascending, alphabetical,
                    App.MViewModel.Songs);
            }

            UpdateSortIndicator();
        }

        private string _currentSortKey = "GSongTitle|SongTitle";
        private bool   _sortAscending  = true;

        // ── Constructor ───────────────────────────────────────────────────────

        public SongsTablePage()
            : base(App.MViewModel.Playlists)
        {
            InitializeComponent();
            NavigationHelper.LoadState += NavigationHelper_LoadState;
            NavigationHelper.SaveState += NavigationHelper_SaveState;
            PlaylistHelper.AddPlaylistsToSubItem(AddTo, AddSelectedItemToPlaylistCommand);
            PlaylistHelper.AddPlaylistsToFlyout(AddToBar, AddSelectedItemToPlaylistCommand);
        }

        // ── Navigation ────────────────────────────────────────────────────────

        private void NavigationHelper_LoadState(object sender, LoadStateEventArgs e)
        {
            var (del, direction, alphabetical) = GetSavedSortPreferences("Songs");
            if (!string.IsNullOrEmpty(del))
            {
                _currentSortKey = del;
                _sortAscending  = direction == SortDirection.Ascending;
                CreateViewModel(del, direction, alphabetical, App.MViewModel.Songs);
            }
            else
            {
                // Default: alphabetical by title, ascending — same as SongsPage
                CreateViewModel("GSongTitle|SongTitle", SortDirection.Ascending, true,
                    App.MViewModel.Songs);
            }

            UpdateMaxPlayCount();
            UpdateStatusBar();
            UpdateSortIndicator();
        }

        private void NavigationHelper_SaveState(object sender, SaveStateEventArgs e)
            => SaveSortingPreferences("Songs");

        // ── Column header click (shared handler via Tag) ──────────────────────

        private void Hdr_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string key)
                SortCommand.Execute(key);
        }

        // ── Status + indicators ───────────────────────────────────────────────

        private void UpdateStatusBar()
        {
            int count = App.MViewModel.Songs.Count;
            var total = App.MViewModel.Songs
                .Select(s => s.Length)
                .Aggregate(TimeSpan.Zero, (a, b) => a + b);

            StatusText.Text = $"{count:N0} songs · {(int)total.TotalHours}h {total.Minutes}m";
        }

        private void UpdateMaxPlayCount()
        {
            MaxPlayCount = App.MViewModel.Songs.Count > 0
                ? Math.Max(1, App.MViewModel.Songs.Max(s => s.PlayCount))
                : 1;
        }

        private void UpdateSortIndicator()
        {
            // Friendly name from the key (strip grouping prefix if present)
            string display = _currentSortKey.Split('|')[0]
                .Replace("GSong", string.Empty)
                .Replace("Song",  string.Empty);
            SortIndicator.Text = $"Sorted by {display} {(_sortAscending ? "↑" : "↓")}";
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void MainList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is SongViewModel song)
                MediaViewModel.PlayFromItemCommand.Execute(song);
        }

        private void MenuFlyout_Opening(object sender, object e)
        {
            var fl   = sender as MenuFlyout;
            var cont = MainList.ItemFromContainer(fl.Target);
            if (cont == null) fl.Hide();
            else SelectedItem = (SongViewModel)cont;
        }

        private async void Remove_Click(object sender, RoutedEventArgs e)
        {
            var svm = SelectedItem;
            ContentDialog dialog = new()
            {
                Title               = ResourceHelper.GetString("DeleteSong"),
                Content             = string.Format(ResourceHelper.GetString("ConfirmRemovalSong"), svm.Title),
                PrimaryButtonStyle  = Resources["AccentButtonStyle"] as Style,
                PrimaryButtonText   = ResourceHelper.GetString("DeleteAnyway"),
                SecondaryButtonText = ResourceHelper.GetString("Close"),
                XamlRoot            = XamlRoot,
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
                await MViewModel.RemoveSongAsync(svm, false);
        }
    }
}
