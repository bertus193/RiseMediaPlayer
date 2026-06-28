using Rise.Common.Constants;
using Rise.Common.Extensions;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Rise.App.Views
{
    /// <summary>
    /// A page that shows details about crashes when the app is
    /// relaunched after a crash happening.
    /// </summary>
    public sealed partial class CrashDetailsPage : Page
    {
        private string Text;

        public CrashDetailsPage()
        {
            InitializeComponent();
        }

        public static Task<bool> TryShowAsync(string crashDetails)
            => ViewHelpers.OpenViewAsync<CrashDetailsPage>(crashDetails, new(500, 500));

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Text = e.Parameter as string;
            base.OnNavigatedTo(e);
        }
    }

    // Event handlers
    public sealed partial class CrashDetailsPage
    {
        private void SubmitIssueButton_Click(object sender, RoutedEventArgs e)
            => _ = URLs.Feedback.LaunchAsync();
    }
}
