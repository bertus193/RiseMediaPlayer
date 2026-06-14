using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rise.Common.Extensions.Markup;
using Rise.Common.Helpers;
using System;

namespace Rise.App.Views
{
    public sealed partial class NoMediaFound : Page
    {
        private readonly NavigationHelper _navigationHelper;

        public NoMediaFound()
        {
            InitializeComponent();
            _navigationHelper = new NavigationHelper(this);

            // WinUI 3: title bar configured centrally in MainWindow
            // Button colours and drag region are already set by MainWindow.ConfigureTitleBar()
            App.MainAppWindow.SetTitleBarElement(AppTitleBar);
        }
    }
}
