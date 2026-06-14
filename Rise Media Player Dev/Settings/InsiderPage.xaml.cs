using Rise.App.Dialogs;
using Rise.Common.Constants;
using Rise.Common.Extensions;
using Rise.Common.Extensions.Markup;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Rise.App.Settings
{
    public sealed partial class InsiderPage : Page
    {
        public InsiderPage()
        {
            InitializeComponent();
        }

        private void ExpanderControl_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(InsiderWallpapers));
        }
    }
}
