using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Rise.Data.ViewModels;
using System.Threading.Tasks;

namespace Rise.App.Views
{
    /// <summary>
    /// Compact Overlay (picture-in-picture) now playing page.
    /// </summary>
    public sealed partial class CompactNowPlayingPage : Page
    {
        private MediaPlaybackViewModel MPViewModel => App.MPViewModel;

        public CompactNowPlayingPage()
        {
            InitializeComponent();

            // Register TitleBar as drag region for this window
            App.MainAppWindow.SetTitleBarElement(TitleBar);

            _ = VisualStateManager.GoToState(this, nameof(PointerOutState), false);
        }

        private void OnPlayerLoaded(object sender, RoutedEventArgs e)
            => MainPlayer.SetMediaPlayer(MPViewModel.Player);

        private void OnPageUnloaded(object sender, RoutedEventArgs e)
            => MainPlayer.SetMediaPlayer(null);

        /// <summary>
        /// Enters Compact Overlay mode and navigates to this page.
        /// Replaces ApplicationView.TryEnterViewModeAsync(ApplicationViewMode.CompactOverlay).
        /// </summary>
        public static Task NavigateAsync(Frame frame)
        {
            // WinUI 3: set CompactOverlay presenter on the AppWindow
            App.MainAppWindow.EnterCompactOverlay();

            _ = frame.Navigate(typeof(CompactNowPlayingPage), null,
                new SuppressNavigationTransitionInfo());

            return Task.CompletedTask;
        }
    }

    // Event handlers
    public sealed partial class CompactNowPlayingPage
    {
        private void OnExitButtonClick(object sender, RoutedEventArgs e)
        {
            // WinUI 3: restore default windowed presenter
            App.MainAppWindow.ExitCompactOverlay();
            Frame.GoBack();
        }

        private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
            => _ = VisualStateManager.GoToState(this, nameof(PointerInState), true);

        private void OnPointerExited(object sender, PointerRoutedEventArgs e)
            => _ = VisualStateManager.GoToState(this, nameof(PointerOutState), true);
    }
}
