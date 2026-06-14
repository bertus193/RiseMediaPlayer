using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Rise.App.Dialogs;
using Rise.App.Helpers;
using Rise.App.Settings;
using Rise.App.UserControls;
using Rise.App.ViewModels;
using Rise.Common.Constants;
using Rise.Common.Enums;
using Rise.Common.Extensions;
using Rise.Common.Extensions.Markup;
using Rise.Common.Helpers;
using Rise.Common.Interfaces;
using Rise.Data.Json;
using Rise.Data.Navigation;
using Rise.Data.ViewModels;
using Rise.NewRepository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Playback;
using Windows.UI;

namespace Rise.App.Views
{
    /// <summary>
    /// Main app page, hosts the NavigationView and ContentFrame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private static bool _loaded;

        private MainViewModel MViewModel => App.MViewModel;
        private SettingsViewModel SViewModel => App.SViewModel;
        private MediaPlaybackViewModel MPViewModel => App.MPViewModel;
        private LastFMViewModel LMViewModel => App.LMViewModel;

        private JsonBackendController<PlaylistViewModel> PBackend
            => App.MViewModel.PBackend;
        private NavigationDataSource NavDataSource => App.NavDataSource;

        private static readonly DependencyProperty RightClickedItemProperty
            = DependencyProperty.Register(nameof(RightClickedItem), typeof(NavigationItemBase),
                typeof(MainPage), null);
        private NavigationItemBase RightClickedItem
        {
            get => (NavigationItemBase)GetValue(RightClickedItemProperty);
            set => SetValue(RightClickedItemProperty, value);
        }

        private static string _navState;

        private readonly Dictionary<string, Type> Destinations = new()
        {
            { "HomePage", typeof(HomePage) },
            { "PlaylistsPage", typeof(PlaylistsPage) },
            { "SongsPage", typeof(SongsPage) },
            { "ArtistsPage", typeof(ArtistsPage) },
            { "AlbumsPage", typeof(AlbumsPage) },
            { "LocalVideosPage", typeof(LocalVideosPage) },
            { "ITunesMigrationPage", typeof(ITunesMigrationPage) },
            { "SongsTablePage", typeof(SongsTablePage) }
        };

        private readonly Dictionary<string, Type> UnlistedDestinations = new()
        {
            { "PlaylistsPage", typeof(PlaylistDetailsPage) },
            { "ArtistsPage", typeof(ArtistSongsPage) },
            { "AlbumsPage", typeof(AlbumSongsPage) },
            { "GenresPage", typeof(GenreSongsPage) }
        };

        private DependencyPropertyWatcher<bool> QueueCheckedWatcher;

        public MainPage()
        {
            InitializeComponent();

            SuspensionManager.RegisterFrame(ContentFrame, "NavViewFrame");

            MViewModel.IndexingStarted += MViewModel_IndexingStarted;
            MViewModel.IndexingFinished += MViewModel_IndexingFinished;
            MViewModel.MetadataFetchingStarted += MViewModel_MetadataFetchingStarted;

            MPViewModel.PlayingItemChanged += MPViewModel_PlayingItemChanged;

            // WinUI 3: register AppTitleBar as the custom drag region through MainWindow
            App.MainAppWindow.SetTitleBarElement(AppTitleBar);

            // Title bar layout is driven by AppWindow.TitleBar.RightInset
            App.MainAppWindow.AppWindow.TitleBar.Changed += AppTitleBar_Changed;

            UpdateTitleBarLayout();

            var date = DateTime.Now;
            if (date.Month == 4 && date.Day == 1)
                RiseSpan.Text = "Rice";

            SetupNavigation();
        }

        private void SetupNavigation()
        {
            NavDataSource.PopulateGroups();
            var playlists = (NavigationItemDestination)NavDataSource.GetItem("PlaylistsPage");
            playlists.Children = PBackend.Items;
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs args)
        {
            IndexingTip.Visibility = Visibility.Collapsed;
            UpdateTitleBarItems(NavView);

            if (!_loaded)
            {
                _loaded = true;

                if (ContentFrame.Content == null)
                    ContentFrame.Navigate(Destinations[SViewModel.Open]);

                await App.InitializeChangeTrackingAsync();

                if (SViewModel.IndexingAtStartupEnabled || SViewModel.IsFirstLaunch)
                {
                    SViewModel.IsFirstLaunch = false;

                    await Task.Delay(300);
                    _ = VisualStateManager.GoToState(this, "ScanningState", false);

                    await Task.Run(MViewModel.StartFullCrawlAsync);
                    return;
                }
                else
                {
                    if (SViewModel.FetchOnlineData)
                    {
                        await Task.Delay(300);
                        _ = VisualStateManager.GoToState(this, "FetchingMetadataState", false);
                        await MViewModel.FetchArtistsArtAsync();
                    }

                    await MViewModel.HandleLibraryChangesAsync(ChangedLibraryType.Both, true);
                    await Repository.UpsertQueuedAsync();
                    await Repository.DeleteQueuedAsync();

                    MViewModel_IndexingFinished(null, null);
                }
            }

            if (MViewModel.IsScanning)
                _ = VisualStateManager.GoToState(this, "ScanningState", false);
        }

        private void OnPageUnloaded(object sender, RoutedEventArgs e)
        {
            App.MainAppWindow.AppWindow.TitleBar.Changed -= AppTitleBar_Changed;

            MViewModel.IndexingStarted -= MViewModel_IndexingStarted;
            MViewModel.IndexingFinished -= MViewModel_IndexingFinished;
            MViewModel.MetadataFetchingStarted -= MViewModel_MetadataFetchingStarted;

            MPViewModel.MediaPlayerRecreated -= OnMediaPlayerRecreated;
            MPViewModel.PlayingItemChanged -= MPViewModel_PlayingItemChanged;

            QueueCheckedWatcher?.Dispose();

            enterFullScreenCommand = null;
            addToPlaylistCommand = null;
            goToNowPlayingCommand = null;

            Bindings.StopTracking();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (!string.IsNullOrEmpty(_navState))
                ContentFrame.SetNavigationState(_navState);

            if (MPViewModel.PlayerCreated)
                InitializePlayerElement(MPViewModel.Player);
            else
                MPViewModel.MediaPlayerRecreated += OnMediaPlayerRecreated;

            await HandleViewModelColorSettingAsync();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            _navState = ContentFrame.GetNavigationState();
        }

        // ── Title Bar ─────────────────────────────────────────────────────────

        private void AppTitleBar_Changed(Microsoft.UI.Windowing.AppWindowTitleBar sender, object args)
            => UpdateTitleBarLayout();

        /// <summary>
        /// Adjusts the custom title bar margins so content never overlaps
        /// the system caption buttons (min/max/close).
        /// RightInset is the WinUI 3 equivalent of CoreApplicationViewTitleBar.SystemOverlayRightInset.
        /// </summary>
        private void UpdateTitleBarLayout()
        {
            double rightInset = App.MainAppWindow.AppWindow.TitleBar.RightInset;

            var currMargin = AppTitleBar.Margin;
            AppTitleBar.Margin = new Thickness(
                currMargin.Left, currMargin.Top, rightInset, currMargin.Bottom);

            currMargin = ControlsPanel.Margin;
            ControlsPanel.Margin = new Thickness(
                currMargin.Left, currMargin.Top, rightInset, currMargin.Bottom);
        }

        private void UpdateTitleBarItems(Microsoft.UI.Xaml.Controls.NavigationView navView)
        {
            var currMargin = AppTitleBar.Margin;

            if (navView.DisplayMode == Microsoft.UI.Xaml.Controls.NavigationViewDisplayMode.Minimal)
            {
                AppTitleBar.Margin = new Thickness(88, currMargin.Top, currMargin.Right, currMargin.Bottom);
                ControlsPanel.Margin = new Thickness(136, currMargin.Top, currMargin.Right, currMargin.Bottom);
            }
            else
            {
                AppTitleBar.Margin = new Thickness(40, currMargin.Top, currMargin.Right, currMargin.Bottom);
                ControlsPanel.Margin = new Thickness(260, currMargin.Top, currMargin.Right, currMargin.Bottom);
            }
        }

        // ── Media Player ──────────────────────────────────────────────────────

        private async void MPViewModel_PlayingItemChanged(object sender, MediaPlaybackItem e)
        {
            // DispatcherQueue replaces Dispatcher.RunAsync
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal,
                async () => await HandleViewModelColorSettingAsync());

            if (MPViewModel.PlayingItemType == MediaPlaybackType.Music)
                _ = await LMViewModel.TryScrobbleItemAsync(e);
        }

        private void OnContentFrameSizeChanged(object sender, SizeChangedEventArgs e)
        {
            switch (e.NewSize.Width)
            {
                case >= 725:
                    VisualStateManager.GoToState(this, "WideContentAreaLayout", true);
                    break;
                case >= 550:
                    VisualStateManager.GoToState(this, "MediumContentAreaLayout", true);
                    break;
                default:
                    VisualStateManager.GoToState(this, "NarrowContentAreaLayout", true);
                    break;
            }
        }

        private void OnMediaPlayerRecreated(object sender, MediaPlayer e)
        {
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal,
                () => InitializePlayerElement(e));
        }

        private void InitializePlayerElement(MediaPlayer player)
        {
            MainPlayer.SetMediaPlayer(player);

            QueueCheckedWatcher = new(PlayerControls, RiseMediaTransportControls.IsQueueButtonCheckedProperty);
            QueueCheckedWatcher.PropertyChanged += OnQueueCheckedChanged;
        }

        private void OnQueueCheckedChanged(DependencyPropertyWatcher<bool> sender, bool newValue)
        {
            if (newValue)
            {
                var queueButton = MainPlayer.FindDescendant<AppBarToggleButton>(a => a.Name == "QueueButton");
                if (queueButton != null)
                    QueueFlyout.ShowAt(queueButton);
            }
        }

        private void QueueFlyout_Closed(object sender, object e)
            => PlayerControls.IsQueueButtonChecked = false;

        // ── Commands ──────────────────────────────────────────────────────────

        [RelayCommand]
        private void EnterFullScreen()
        {
            if (MPViewModel.PlayingItem == null) return;

            // WinUI 3: use AppWindow presenter instead of ApplicationView
            App.MainAppWindow.EnterFullScreen();
            Frame.Navigate(typeof(NowPlayingPage), true);
        }

        [RelayCommand]
        private async Task AddToPlaylistAsync(PlaylistViewModel playlist)
        {
            var playlistHelper = new AddToPlaylistHelper(App.MViewModel.Playlists);

            IMediaItem mediaItem = null;

            if (MPViewModel.PlayingItemType == MediaPlaybackType.Music)
                mediaItem = MViewModel.Songs.FirstOrDefault(s => s.Location == MPViewModel.PlayingItemProperties.Location);
            else if (MPViewModel.PlayingItemType == MediaPlaybackType.Video)
                mediaItem = MViewModel.Videos.FirstOrDefault(v => v.Location == MPViewModel.PlayingItemProperties.Location);

            if (mediaItem == null)
            {
                if (MPViewModel.PlayingItemType == MediaPlaybackType.Music)
                    mediaItem = await MPViewModel.PlayingItem.AsSongAsync();
                else if (MPViewModel.PlayingItemType == MediaPlaybackType.Video)
                    mediaItem = await MPViewModel.PlayingItem.AsVideoAsync();
            }

            if (playlist == null)
                await playlistHelper.CreateNewPlaylistAsync(mediaItem);
            else
            {
                playlist.AddItem(mediaItem);
                await PBackend.SaveAsync();
            }
        }

        /// <summary>
        /// NowPlaying navigation. <paramref name="compact"/> true = Compact Overlay.
        /// Replaces the old ApplicationViewMode parameter.
        /// </summary>
        [RelayCommand]
        private Task GoToNowPlayingAsync(bool compact = false)
        {
            if (MPViewModel.PlayingItem != null)
            {
                if (compact)
                    return CompactNowPlayingPage.NavigateAsync(Frame);
                else
                    _ = Frame.Navigate(typeof(NowPlayingPage), null, new DrillInNavigationTransitionInfo());
            }

            return Task.CompletedTask;
        }

        private async void OnDisplayItemClick(object sender, RoutedEventArgs e)
            => await GoToNowPlayingAsync(false);

        private void OnDisplayItemRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (MPViewModel.PlayingItem == null) return;
            if (MPViewModel.PlayingItemType == MediaPlaybackType.Video)
                PlayingItemVideoFlyout.ShowAt(MainPlayer);
            else
                PlayingItemMusicFlyout.ShowAt(MainPlayer);
        }

        // ── Indexing state ────────────────────────────────────────────────────

        private void MViewModel_IndexingStarted(object sender, EventArgs e)
        {
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal,
                async () =>
                {
                    await Task.Delay(60);
                    _ = VisualStateManager.GoToState(this, "ScanningState", false);
                });
        }

        private void MViewModel_MetadataFetchingStarted(object sender, EventArgs e)
        {
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal,
                () => _ = VisualStateManager.GoToState(this, "FetchingMetadataState", false));
        }

        private void MViewModel_IndexingFinished(object sender, IndexingFinishedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal,
                async () =>
                {
                    _ = VisualStateManager.GoToState(this, "ScanningDoneState", false);
                    await Task.Delay(2500);
                });
        }

        private void NavigationViewControl_DisplayModeChanged(
            Microsoft.UI.Xaml.Controls.NavigationView sender,
            Microsoft.UI.Xaml.Controls.NavigationViewDisplayModeChangedEventArgs args)
            => UpdateTitleBarItems(sender);

        // ── Color / Glaze ─────────────────────────────────────────────────────

        public async Task HandleViewModelColorSettingAsync()
        {
            if (SViewModel.SelectedGlaze == GlazeTypes.MediaThumbnail)
            {
                if (MPViewModel.PlayingItem != null)
                {
                    using var stream = await MPViewModel
                        .PlayingItemProperties.Thumbnail.OpenReadAsync();

                    var decoder = await BitmapDecoder.CreateAsync(stream);
                    var colorThief = new ColorThiefDotNet.ColorThief();

                    var stolen = (await colorThief.GetColor(decoder)).Color;
                    SViewModel.GlazeColors = Color.FromArgb(25, stolen.R, stolen.G, stolen.B);
                }
                else
                {
                    SViewModel.GlazeColors = Colors.Transparent;
                }
            }
        }

        // ── Navigation ────────────────────────────────────────────────────────

        private void OnNavigated(object sender, NavigationEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.New)
                return;

            var type = ContentFrame.CurrentSourcePageType;
            bool hasKey = Destinations.TryGetKey(type, out string key);

            if (!hasKey)
                hasKey = UnlistedDestinations.TryGetKey(type, out key);

            if (hasKey)
            {
                var item = NavDataSource.GetItem(key);
                if (item != null)
                    NavView.SelectedItem = item;
            }
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
            => throw new Exception("Failed to load Page " + e.SourcePageType.FullName);

        private void NavigationView_ItemInvoked(
            Microsoft.UI.Xaml.Controls.NavigationView sender,
            Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs args)
        {
            var invoked = args.InvokedItemContainer?.Tag;
            if (invoked is NavigationItemBase item)
            {
                string id = item.Id;
                if (ContentFrame.SourcePageType != Destinations[id])
                    ContentFrame.Navigate(Destinations[id], null, args.RecommendedNavigationTransitionInfo);
            }
            else if (invoked is PlaylistViewModel playlist)
            {
                ContentFrame.Navigate(typeof(PlaylistDetailsPage),
                    playlist.Id, args.RecommendedNavigationTransitionInfo);
            }
        }

        private void NavigationViewItem_AccessKeyInvoked(UIElement sender, AccessKeyInvokedEventArgs args)
        {
            var elm = sender as FrameworkElement;
            if (elm?.Tag is NavigationItemBase item)
            {
                string id = item.Id;
                var pageType = Destinations[id];
                if (ContentFrame.SourcePageType != pageType)
                    ContentFrame.Navigate(pageType);
            }
        }

        private void NavigationView_BackRequested(
            Microsoft.UI.Xaml.Controls.NavigationView sender,
            Microsoft.UI.Xaml.Controls.NavigationViewBackRequestedEventArgs args)
            => ContentFrame.GoBack();

        // ── Search ────────────────────────────────────────────────────────────

        private void OnSearchQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
            => _ = ContentFrame.Navigate(typeof(SearchResultsPage), sender.Text);

        private async void OnSearchSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            var searchItem = args.SelectedItem as SearchItemViewModel;
            sender.Text = searchItem.Title;
            sender.IsSuggestionListOpen = false;

            switch (searchItem.ItemType)
            {
                case "Album":
                    var album = App.MViewModel.Albums.FirstOrDefault(a => a.Title.Equals(searchItem.Title));
                    _ = ContentFrame.Navigate(typeof(AlbumSongsPage), album.Model.Id);
                    break;
                case "Song":
                    var song = App.MViewModel.Songs.FirstOrDefault(s => s.Title.Equals(searchItem.Title));
                    await MPViewModel.PlaySingleItemAsync(song);
                    break;
                case "Artist":
                    var artist = App.MViewModel.Artists.FirstOrDefault(a => a.Name.Equals(searchItem.Title));
                    ContentFrame.Navigate(typeof(ArtistSongsPage), artist.Model.Id);
                    break;
            }
        }

        private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;

            var suitableItems = new List<SearchItemViewModel>();
            string[] splitText = sender.Text.ToLower().Split(" ");
            int maxCount = 4;

            foreach (var album in MViewModel.Albums)
                if (splitText.All(k => album.Title.ToLower().Contains(k)) && suitableItems.Count < maxCount)
                    suitableItems.Add(new SearchItemViewModel { Title = album.Title, Subtitle = $"{album.Artist} - {album.Genres}", ItemType = "Album", Thumbnail = album.Thumbnail });

            foreach (var song in MViewModel.Songs)
                if (splitText.All(k => song.Title.ToLower().Contains(k)) && suitableItems.Count < maxCount)
                    suitableItems.Add(new SearchItemViewModel { Title = song.Title, Subtitle = $"{song.Artist} - {song.Genres}", ItemType = "Song", Thumbnail = song.Thumbnail });

            foreach (var artist in MViewModel.Artists)
                if (splitText.All(k => artist.Name.ToLower().Contains(k)) && suitableItems.Count < maxCount)
                    suitableItems.Add(new SearchItemViewModel { Title = artist.Name, ItemType = "Artist", Thumbnail = artist.Picture });

            sender.ItemsSource = suitableItems;
        }

        // ── Misc UI ───────────────────────────────────────────────────────────

        public static Visibility IsStringEmpty(string str)
            => string.IsNullOrWhiteSpace(str) ? Visibility.Collapsed : Visibility.Visible;

        private async void Feedback_Click(object sender, RoutedEventArgs e)
            => await URLs.NewIssue.LaunchAsync();

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
            => Frame.Navigate(typeof(AllSettingsPage));

        private void NavigationViewItem_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var elm = sender as FrameworkElement;
            var item = elm?.Tag as NavigationItemDestination;

            string flyoutId = item?.FlyoutId;
            if (!string.IsNullOrEmpty(flyoutId))
            {
                RightClickedItem = item;
                if (flyoutId == "DefaultItemFlyout")
                {
                    bool up = NavDataSource.CanMoveUp(item);
                    bool down = NavDataSource.CanMoveDown(item);

                    TopOption.IsEnabled = up;
                    UpOption.IsEnabled = up;
                    DownOption.IsEnabled = down;
                    BottomOption.IsEnabled = down;
                }

                var flyout = Resources[flyoutId] as MenuFlyout;
                if (args.TryGetPosition(sender, out var point))
                    flyout.ShowAt(sender, point);
                else
                    flyout.ShowAt(elm);
            }

            args.Handled = true;
        }

        private async void Account_Click(object sender, RoutedEventArgs e)
        {
            if (LMViewModel.Authenticated)
                _ = await ("https://www.last.fm/user/" + LMViewModel.Username).LaunchAsync();
            else
                Frame.Navigate(typeof(AllSettingsPage));
        }

        private void AddedTip_ActionButtonClick(Microsoft.UI.Xaml.Controls.TeachingTip sender, object args)
            => _ = Frame.Navigate(typeof(AllSettingsPage));

        private void OnAlbumButtonClick(object sender, RoutedEventArgs e)
        {
            if (MPViewModel.PlayingItemType != MediaPlaybackType.Music) return;
            var album = MViewModel.Albums.FirstOrDefault(a => a.Model.Title == MPViewModel.PlayingItemProperties.Album);
            if (album != null)
                ContentFrame.Navigate(typeof(AlbumSongsPage), album.Model.Id);
            PlayingItemMusicFlyout.Hide();
        }

        private void OnArtistButtonClick(object sender, RoutedEventArgs e)
        {
            if (MPViewModel.PlayingItemType != MediaPlaybackType.Music) return;
            var artist = MViewModel.Artists.FirstOrDefault(a => a.Model.Name == MPViewModel.PlayingItemProperties.Artist);
            if (artist != null)
                ContentFrame.Navigate(typeof(ArtistSongsPage), artist.Model.Id);
            PlayingItemMusicFlyout.Hide();
        }

        private void GoToScanningSettings_Click(object sender, RoutedEventArgs e)
            => _ = Frame.Navigate(typeof(AllSettingsPage));

        private void DismissButton_Click(object sender, RoutedEventArgs e)
            => _ = VisualStateManager.GoToState(this, "NotScanningState", false);
    }
}
