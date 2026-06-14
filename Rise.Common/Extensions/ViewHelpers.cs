using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Windows.Foundation;
using WinRT.Interop;

namespace Rise.Common.Extensions
{
    public static class ViewHelpers
    {
        /// <summary>
        /// Opens a new <see cref="AppWindow"/> hosting a frame
        /// that navigates to <typeparamref name="TPage"/> with the provided parameter.
        /// </summary>
        public static async Task<bool> OpenViewAsync<TPage>(object parameter = null, Size minSize = default, bool useMinSize = true)
            where TPage : Page
        {
            var window = new Window();
            var frame = new Frame();
            frame.Navigate(typeof(TPage), parameter);
            window.Content = frame;

            var appWindow = GetAppWindow(window);
            if (appWindow != null && useMinSize && minSize != default)
            {
                appWindow.Resize(new Windows.Graphics.SizeInt32((int)minSize.Width, (int)minSize.Height));
            }

            window.Activate();
            return await Task.FromResult(true);
        }

        private static AppWindow GetAppWindow(Window window)
        {
            var hwnd = WindowNative.GetWindowHandle(window);
            var wndId = Win32Interop.GetWindowIdFromWindow(hwnd);
            return AppWindow.GetFromWindowId(wndId);
        }
    }
}
