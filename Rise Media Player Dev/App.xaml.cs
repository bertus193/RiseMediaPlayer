using CommunityToolkit.WinUI.Notifications;
using Microsoft.QueryStringDotNET;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rise.App.ViewModels;
using Rise.App.Views;
using Rise.Common.Constants;
using Rise.Common.Enums;
using Rise.Common.Extensions;
using Rise.Common.Extensions.Markup;
using Rise.Common.Helpers;
using Rise.Data.Messages;
using Rise.Data.Navigation;
using Rise.Data.ViewModels;
using Rise.Effects;
using Rise.NewRepository;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Notifications;
using WinRT.Interop;

namespace Rise.App
{
    public partial class App : Application
    {
        private static Timer IndexingTimer;

        // The main window - held alive for the app's lifetime
        internal static MainWindow MainAppWindow { get; private set; }

        // No lazy init (used very early on)
        public static MainViewModel MViewModel { get; } = new();
        public static SettingsViewModel SViewModel { get; } = new();
        public static NavigationDataSource NavDataSource { get; } = new();

        // Lazy init
        private readonly static Lazy<MediaPlaybackViewModel> _mpViewModel
            = new(OnMPViewModelRequested);
        public static MediaPlaybackViewModel MPViewModel => _mpViewModel.Value;

        private readonly static Lazy<LastFMViewModel> _lmViewModel
            = new(OnLFMRequested);
        public static LastFMViewModel LMViewModel => _lmViewModel.Value;

        private readonly static Lazy<StorageLibrary> _musicLibrary
            = new(OnStorageLibraryRequested(KnownLibraryId.Music));
        public static StorageLibrary MusicLibrary => _musicLibrary.Value;

        private readonly static Lazy<StorageLibrary> _videoLibrary
            = new(OnStorageLibraryRequested(KnownLibraryId.Videos));
        public static StorageLibrary VideoLibrary => _videoLibrary.Value;

        public App()
        {
            int theme = SViewModel.Theme;
            if (theme == 0)
                RequestedTheme = ApplicationTheme.Light;
            else if (theme == 1)
                RequestedTheme = ApplicationTheme.Dark;

            // Reset the glaze color before startup if necessary
            if (SViewModel.SelectedGlaze == GlazeTypes.MediaThumbnail)
                SViewModel.GlazeColors = Colors.Transparent;

            InitializeComponent();

            UnhandledException += OnUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            MainAppWindow = new MainWindow();
            await ActivateAsync(false);
        }

        /// <summary>
        /// Handles file activation - called from MainWindow when app receives files.
        /// </summary>
        internal static async Task HandleFileActivationAsync(IStorageItem[] files)
        {
            var storageFiles = files.OfType<StorageFile>();
            await MPViewModel.PlayFilesAsync(storageFiles);
        }

        private async Task ActivateAsync(bool prelaunched)
        {
            var rootFrame = MainAppWindow.RootFrame;

            if (rootFrame.Content == null)
            {
                rootFrame.NavigationFailed += OnNavigationFailed;
                rootFrame.AllowDrop = true;

                await Repository.InitializeDatabaseAsync();

                if (!prelaunched)
                {
                    _ = !SViewModel.SetupCompleted
                        ? rootFrame.Navigate(typeof(SetupPage))
                        : rootFrame.Navigate(typeof(MainPage));
                }
            }

            MainAppWindow.Activate();
        }
    }

    // Data source/ViewModel initialization
    public partial class App
    {
        private static LastFMViewModel OnLFMRequested()
        {
            var lfm = new LastFMViewModel(LastFM.Key, LastFM.Secret);
            lfm.TryLoadCredentials(LastFM.VaultResource);
            return lfm;
        }

        private static MediaPlaybackViewModel OnMPViewModelRequested()
        {
            // Register EqualizerEffect as an in-process WinRT activatable class.
            // Must happen before AddAudioEffect is called on MediaPlayer.
            EqualizerEffectActivation.Register();

            var mpvm = new MediaPlaybackViewModel();

            if (!EqualizerEffect.Initialized)
            {
                var eq = EqualizerEffect.Current;
                eq.InitializeBands(SViewModel.EqualizerGain);
                eq.IsEnabled = SViewModel.EqualizerEnabled;
            }

            mpvm.AddEffect(new(typeof(EqualizerEffect), false, true, null));
            return mpvm;
        }

        private static StorageLibrary OnStorageLibraryRequested(KnownLibraryId id)
        {
            var library = StorageLibrary.GetLibraryAsync(id).Get();
            library.ChangeTracker.Enable();
            return library;
        }
    }

    // Indexing
    public partial class App
    {
        public static async Task InitializeChangeTrackingAsync()
        {
            if (SViewModel.IndexingFileTrackingEnabled)
            {
                _ = await MusicLibrary.TrackBackgroundAsync($"{nameof(MusicLibrary)} background tracker");
                var result = await VideoLibrary.TrackBackgroundAsync($"{nameof(VideoLibrary)} background tracker");

                if (result == BackgroundTaskRegistrationStatus.Successful ||
                    result == BackgroundTaskRegistrationStatus.AlreadyExists)
                {
                    MusicLibrary.DefinitionChanged += OnLibraryDefinitionChanged;
                    VideoLibrary.DefinitionChanged += OnLibraryDefinitionChanged;
                    return;
                }
            }

            RestartIndexingTimer();
        }

        private static async void OnLibraryDefinitionChanged(StorageLibrary sender, object args)
        {
            await MViewModel.StartFullCrawlAsync();
        }

        public static void RestartIndexingTimer()
        {
            if (IndexingTimer != null && IndexingTimer.Enabled)
                IndexingTimer.Stop();

            if (!SViewModel.IndexingTimerEnabled)
                return;

            var span = TimeSpan.FromMinutes(SViewModel.IndexingTimerInterval);
            IndexingTimer = new(span.TotalMilliseconds)
            {
                AutoReset = true
            };

            IndexingTimer.Elapsed += IndexingTimer_Elapsed;
            IndexingTimer.Start();
        }

        private static async void IndexingTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            await MViewModel.HandleLibraryChangesAsync(ChangedLibraryType.Both, true);
            await Repository.UpsertQueuedAsync();
            await Repository.DeleteQueuedAsync();
        }
    }

    // Error handling
    public partial class App
    {
        private void ShowExceptionToast(Exception e)
        {
            string notifTitle = ResourceHelper.GetString("ErrorOcurred");
            ToastContent content = new ToastContentBuilder()
                .AddToastActivationInfo(new QueryString()
                {
                     { "stackTrace", e.StackTrace },
                     { "message", e.Message },
                     { "exceptionName", e.GetType().ToString() },
                     { "source", e.Source },
                     { "hresult", $"{e.HResult}" }
                }.ToString(), ToastActivationType.Foreground)
                .AddText(notifTitle)
                .AddText(ResourceHelper.GetString("CrashStackTrace"))
                .GetToastContent();

            ToastNotification notification = new(content.GetXml());
            ToastNotificationManager.CreateToastNotifier().Show(notification);

            var builder = new StringBuilder();
            builder.Append(ResourceHelper.GetString("CrashDetails"));
            builder.Append("\n\n");
            builder.AppendLine("-----");
            builder.Append("Exception type: ");
            builder.AppendLine(e.GetType().ToString());
            builder.Append("HRESULT: ");
            builder.AppendLine(e.HResult.ToString());
            builder.Append("Source: ");
            builder.AppendLine(e.Source);
            builder.AppendLine();
            builder.AppendLine("Message:");
            builder.AppendLine(e.Message);
            builder.AppendLine();
            builder.AppendLine("Stack trace:");
            builder.AppendLine(e.StackTrace);
            builder.AppendLine("-----");

            var notif = new BasicNotification(notifTitle, builder.ToString(), "\uE8BB");
            MViewModel.NBackend.Items.Add(notif);
            MViewModel.NBackend.Save();
        }

        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Exception.WriteToOutput();
            ShowExceptionToast(e.Exception);
        }

        private void OnCurrentDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            (e.ExceptionObject as Exception)?.WriteToOutput();
            ShowExceptionToast(e.ExceptionObject as Exception);
        }

        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            e.Exception.WriteToOutput();
            ShowExceptionToast(e.Exception);
        }

        private void OnNavigationFailed(object sender, Microsoft.UI.Xaml.Navigation.NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }
    }
}
