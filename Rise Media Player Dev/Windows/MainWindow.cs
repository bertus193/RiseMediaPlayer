using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using WinRT.Interop;

namespace Rise.App
{
    /// <summary>
    /// The main application window. Replaces the UWP CoreApplication view model.
    /// Owns the AppWindow, title bar configuration, and file activation routing.
    /// </summary>
    public sealed class MainWindow : Window
    {
        /// <summary>The WinAppSDK AppWindow for this window.</summary>
        public AppWindow AppWindow { get; }

        /// <summary>Root frame used for page navigation.</summary>
        public Frame RootFrame { get; } = new Frame();

        public MainWindow()
        {
            Content = RootFrame;
            Title = "Rise Media Player";

            AppWindow = GetAppWindowForCurrentWindow();
            ConfigureTitleBar();
        }

        // ── Title Bar ──────────────────────────────────────────────────────────

        /// <summary>
        /// Registers a XAML element as the custom drag region.
        /// Called by pages that host a custom title bar element.
        /// </summary>
        public void SetTitleBarElement(UIElement element)
        {
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(element);
        }

        private void ConfigureTitleBar()
        {
            ExtendsContentIntoTitleBar = true;

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = AppWindow.TitleBar;
                titleBar.ExtendsContentIntoTitleBar = true;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            }
        }

        // ── Window Presentation Modes ──────────────────────────────────────────

        /// <summary>Enters full-screen mode.</summary>
        public void EnterFullScreen()
        {
            AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        }

        /// <summary>Exits full-screen mode.</summary>
        public void ExitFullScreen()
        {
            AppWindow.SetPresenter(AppWindowPresenterKind.Default);
        }

        /// <summary>Returns true when the window is in full-screen mode.</summary>
        public bool IsFullScreen
            => AppWindow.Presenter is FullScreenPresenter;

        /// <summary>Enters Compact Overlay (picture-in-picture) mode.</summary>
        public void EnterCompactOverlay()
        {
            AppWindow.SetPresenter(AppWindowPresenterKind.CompactOverlay);
        }

        /// <summary>Returns to the default windowed mode.</summary>
        public void ExitCompactOverlay()
        {
            AppWindow.SetPresenter(AppWindowPresenterKind.Default);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private AppWindow GetAppWindowForCurrentWindow()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var wndId = Win32Interop.GetWindowIdFromWindow(hwnd);
            return AppWindow.GetFromWindowId(wndId);
        }
    }
}
