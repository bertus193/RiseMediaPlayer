using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using Windows.Foundation.Metadata;
using Windows.System;
using Windows.UI.Core;

namespace Rise.Common.Helpers
{
    [WebHostHidden]
    public partial class NavigationHelper : DependencyObject
    {
        private Page Page { get; set; }
        private Frame Frame => Page.Frame;

        public NavigationHelper(Page page)
        {
            Page = page;

            Page.Loaded += (sender, e) =>
            {
                Page.XamlRoot.Content.KeyDown += OnAcceleratorKeyActivated;
                Page.XamlRoot.Content.PointerPressed += CoreWindow_PointerPressed;
            };

            Page.Unloaded += (sender, e) =>
            {
                Page.XamlRoot.Content.KeyDown -= OnAcceleratorKeyActivated;
                Page.XamlRoot.Content.PointerPressed -= CoreWindow_PointerPressed;
            };
        }

        #region Navigation support

        public virtual bool CanGoBack()
            => Frame != null && Frame.CanGoBack;

        public virtual bool CanGoForward()
            => Frame != null && Frame.CanGoForward;

        [RelayCommand(CanExecute = nameof(CanGoBack))]
        public virtual void GoBack()
        {
            if (Frame != null && Frame.CanGoBack) Frame.GoBack();
        }

        [RelayCommand(CanExecute = nameof(CanGoForward))]
        public virtual void GoForward()
        {
            if (Frame != null && Frame.CanGoForward) Frame.GoForward();
        }

        private void OnAcceleratorKeyActivated(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            var virtualKey = e.Key;

            if (virtualKey == VirtualKey.Left || virtualKey == VirtualKey.Right ||
                (int)virtualKey == 166 || (int)virtualKey == 167)
            {
                var downState = CoreVirtualKeyStates.Down;
                bool menuKey = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu).HasFlag(downState);
                bool controlKey = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(downState);
                bool shiftKey = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(downState);
                bool noModifiers = !menuKey && !controlKey && !shiftKey;
                bool onlyAlt = menuKey && !controlKey && !shiftKey;

                if (((int)virtualKey == 166 && noModifiers) ||
                    (virtualKey == VirtualKey.Left && onlyAlt))
                {
                    e.Handled = true;
                    GoBackCommand.Execute(null);
                }
                else if (((int)virtualKey == 167 && noModifiers) ||
                    (virtualKey == VirtualKey.Right && onlyAlt))
                {
                    e.Handled = true;
                    GoForwardCommand.Execute(null);
                }
            }
        }

        private void CoreWindow_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var properties = e.GetCurrentPoint(null).Properties;

            if (properties.IsLeftButtonPressed || properties.IsRightButtonPressed ||
                properties.IsMiddleButtonPressed) return;

            bool backPressed = properties.IsXButton1Pressed;
            bool forwardPressed = properties.IsXButton2Pressed;
            if (backPressed ^ forwardPressed)
            {
                e.Handled = true;
                if (backPressed) GoBackCommand.Execute(null);
                if (forwardPressed) GoForwardCommand.Execute(null);
            }
        }

        #endregion

        #region Process lifetime management

        private string _pageKey;

        public event LoadStateEventHandler LoadState;
        public event SaveStateEventHandler SaveState;

        public void OnNavigatedTo(NavigationEventArgs e)
        {
            var frameState = SuspensionManager.SessionStateForFrame(Frame);
            _pageKey = "Page-" + Frame.BackStackDepth;

            if (e.NavigationMode == NavigationMode.New)
            {
                var nextPageKey = _pageKey;
                int nextPageIndex = Frame.BackStackDepth;
                while (frameState.Remove(nextPageKey))
                {
                    nextPageIndex++;
                    nextPageKey = "Page-" + nextPageIndex;
                }

                LoadState?.Invoke(this, new LoadStateEventArgs(e.Parameter, null));
            }
            else
            {
                LoadState?.Invoke(this, new LoadStateEventArgs(e.Parameter, (Dictionary<string, object>)frameState[_pageKey]));
            }
        }

        public void OnNavigatedFrom(NavigationEventArgs e)
        {
            var frameState = SuspensionManager.SessionStateForFrame(Frame);
            var pageState = new Dictionary<string, object>();
            SaveState?.Invoke(this, new SaveStateEventArgs(pageState));
            frameState[_pageKey] = pageState;
        }

        #endregion
    }

    public delegate void LoadStateEventHandler(object sender, LoadStateEventArgs e);
    public delegate void SaveStateEventHandler(object sender, SaveStateEventArgs e);

    public class LoadStateEventArgs : EventArgs
    {
        public object NavigationParameter { get; private set; }
        public Dictionary<string, object> PageState { get; private set; }

        public LoadStateEventArgs(object navigationParameter, Dictionary<string, object> pageState)
        {
            NavigationParameter = navigationParameter;
            PageState = pageState;
        }
    }

    public class SaveStateEventArgs : EventArgs
    {
        public Dictionary<string, object> PageState { get; private set; }

        public SaveStateEventArgs(Dictionary<string, object> pageState)
        {
            PageState = pageState;
        }
    }
}
