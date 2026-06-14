using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rise.App.Views;
using Rise.ITunesSync.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Rise.App.Views
{
    /// <summary>
    /// One-time iTunes → Rise migration wizard.
    /// Four steps: Locate → Preview → Progress → Summary.
    /// </summary>
    public sealed partial class ITunesMigrationPage : Page
    {
        private string? _libraryPath;
        private MigrationPreview? _preview;
        private CancellationTokenSource? _cts;
        private readonly ITunesMigrationService _service = new();

        public ITunesMigrationPage()
        {
            InitializeComponent();
            TryAutoDetectLibrary();
        }

        // ── Auto-detect ────────────────────────────────────────────────────────

        private void TryAutoDetectLibrary()
        {
            var path = ITunesLibraryParser.TryFindDefaultLibraryPath();
            if (path != null)
                SetLibraryPath(path);
        }

        private void SetLibraryPath(string path)
        {
            _libraryPath = path;
            LibraryPathText.Text = path;
            PreviewButton.IsEnabled = true;
        }

        // ── Step 1 handlers ────────────────────────────────────────────────────

        private async void OnBrowseLibraryClick(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.MusicLibrary,
                FileTypeFilter = { ".xml" },
            };

            // WinUI 3 requires window handle initialisation for pickers
            var hwnd = WindowNative.GetWindowHandle(App.MainAppWindow);
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
                SetLibraryPath(file.Path);
        }

        private async void OnPreviewClick(object sender, RoutedEventArgs e)
        {
            if (_libraryPath == null) return;

            try
            {
                PreviewButton.IsEnabled = false;
                _preview = await Task.Run(() => _service.Preview(_libraryPath));

                // Populate step 2 counters
                TotalTracksText.Text  = _preview.TotalTracks.ToString("N0");
                PlayCountsText.Text   = _preview.TracksWithPlayCount.ToString("N0");
                RatingsText.Text      = _preview.TracksWithRating.ToString("N0");
                PlaylistsText.Text    = _preview.TotalPlaylists.ToString("N0");

                if (_preview.LibraryFolderPath != null)
                    LibraryFolderText.Text = _preview.LibraryFolderPath;
                else
                    LibraryFolderCard.Visibility = Visibility.Collapsed;

                GoToStep(2);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("No se pudo leer la biblioteca", ex.Message);
                PreviewButton.IsEnabled = true;
            }
        }

        // ── Step 2 handlers ────────────────────────────────────────────────────

        private void OnBackToStep1Click(object sender, RoutedEventArgs e)
            => GoToStep(1);

        private async void OnStartMigrationClick(object sender, RoutedEventArgs e)
        {
            if (_libraryPath == null) return;

            _service.ImportPlayCounts = ImportPlayCountsToggle.IsOn;
            _service.ImportRatings    = ImportRatingsToggle.IsOn;
            _service.ImportPlaylists  = ImportPlaylistsToggle.IsOn;
            _service.AddLibraryFolder = AddLibraryFolderToggle.IsOn;

            _cts = new CancellationTokenSource();
            GoToStep(3);

            var progress = new Progress<MigrationProgress>(p =>
            {
                ProgressPhaseText.Text  = p.Phase;
                MigrationProgressBar.Value = p.Percentage;
                ProgressDetailText.Text = p.Total > 0
                    ? $"{p.Current:N0} / {p.Total:N0}"
                    : string.Empty;
            });

            var result = await _service.RunAsync(_libraryPath, progress, _cts.Token);

            GoToStep(4);
            PopulateSummary(result);
        }

        // ── Step 3 handlers ────────────────────────────────────────────────────

        private void OnCancelMigrationClick(object sender, RoutedEventArgs e)
            => _cts?.Cancel();

        // ── Step 4 handlers ────────────────────────────────────────────────────

        private void OnGoToLibraryClick(object sender, RoutedEventArgs e)
            => Frame.Navigate(typeof(SongsPage));

        // ── Summary ────────────────────────────────────────────────────────────

        private void PopulateSummary(MigrationResult result)
        {
            if (result.Error != null)
            {
                ResultIcon.Glyph = "\uEA39";   // Error icon
                ResultIcon.Foreground = (Microsoft.UI.Xaml.Media.Brush)
                    Resources["SystemFillColorCriticalBrush"];
                ResultTitle.Text = result.Error;
                return;
            }

            ResultMatchedText.Text    = result.SongsMatched.ToString("N0");
            ResultRatingsText.Text    = result.RatingsImported.ToString("N0");
            ResultPlayCountsText.Text = result.PlayCountsImported.ToString("N0");
            ResultPlaylistsText.Text  = result.PlaylistsImported.ToString("N0");

            if (result.SongsUnmatched > 0)
            {
                UnmatchedWarning.Visibility = Visibility.Visible;
                UnmatchedText.Text =
                    $"{result.SongsUnmatched:N0} canciones de iTunes no se encontraron en la " +
                    "biblioteca de Rise. Esto ocurre cuando Rise aún no ha indexado la carpeta " +
                    "de música de iTunes. Asegúrate de añadirla en Ajustes → Escaneo.";
            }

            // Trigger a background re-crawl so new stats appear immediately
            _ = App.MViewModel.StartFullCrawlAsync();
        }

        // ── Navigation helper ──────────────────────────────────────────────────

        private void GoToStep(int step)
        {
            Step1Panel.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
            Step2Panel.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
            Step3Panel.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;
            Step4Panel.Visibility = step == 4 ? Visibility.Visible : Visibility.Collapsed;

            StepIndicator.Text = $"Paso {step} de 4";
            StepTitle.Text = step switch
            {
                1 => "Localizar biblioteca de iTunes",
                2 => "Previsualizar importación",
                3 => "Migrando…",
                4 => "Resumen",
                _ => string.Empty,
            };
        }

        private async Task ShowErrorAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title   = title,
                Content = message,
                CloseButtonText = "Cerrar",
                XamlRoot = XamlRoot,
            };
            await dialog.ShowAsync();
        }
    }
}
