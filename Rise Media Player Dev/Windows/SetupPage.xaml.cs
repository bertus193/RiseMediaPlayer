using Rise.App.Dialogs;
using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Rise.App.Views
{
    /// <summary>
    /// Setup page.
    /// </summary>
    public sealed partial class SetupPage : Page
    {
        public SetupPage()
        {
            InitializeComponent();
        }

        private async void SetupButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Content = new SetupDialogContent(),
                FullSizeDesired = true,
            };

            dialog.Resources["ContentDialogMaxWidth"] = (double)762;
            dialog.Resources["ContentDialogMaxHeight"] = (double)490;

            _ = await dialog.ShowAsync();
        }
    }
}
