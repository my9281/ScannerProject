using Scanner.Controllers;
using Scanner.Helpers.Services;
using Scanner.WPF.Helpers;
using Scanner.WPF.Services;
using Scanner.WPF.ViewModels;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Threading;
namespace Scanner.WPF.Controllers
{
    public sealed class MainWindowController : ControllerBase
    {
        private readonly IMainWindowView _view;
        private readonly IDesktopWindows _windows;

        private readonly MainWindowViewModel _viewModel;
        private readonly ChecklistDataCache _baseData;
        private readonly ShelvedPalletApiService _palletApi;
        private bool _scanInitialized;
        private bool _syncing;
        private bool _cacheLoaded;
        private readonly CancellationTokenSource _syncCancellation = new CancellationTokenSource();
        public MainWindowController(IMainWindowView view, ChecklistDataCache baseData, MainWindowViewModel viewModel, IDesktopWindows windows, ShelvedPalletApiService palletApi)
        {
            _view = view;
            _windows = windows;

            _baseData = baseData ?? throw new ArgumentNullException(nameof(baseData));
            _palletApi = palletApi;
            _viewModel = viewModel;
            _view.OwnerWindow.DataContext = _viewModel;
            _viewModel.FocusRequested += ViewModel_FocusRequested;
            _view.OwnerWindow.Activated += MainWindow_Activated;

        }

        public async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_cacheLoaded || _syncing) return;
            _cacheLoaded = true;
            _syncing = true;
            SetInterfaceBusy(true);
            _view.AutoSyncStatus.Text = UiText.Get("WpfSyncCacheLoading");
            try
            {
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
                var result = new BluettiSyncService().RestoreCached(_viewModel.ApplyForecastFile, _baseData);
                if (result.ForecastImported || result.BaseImported || result.State == BluettiSyncState.Failed)
                    _view.AutoSyncStatus.Text = UiText.Get(result.State == BluettiSyncState.Failed ? "WpfSyncCachePartial" : "WpfSyncCacheLoaded") +
                        Environment.NewLine + string.Format(UiText.Get("WpfSyncCounts"), result.ForecastCount, result.BaseCount);
                else _view.AutoSyncStatus.SetResourceReference(System.Windows.Controls.TextBlock.TextProperty, "WpfAutoSyncDescription");
            }
            catch (Exception) { _view.AutoSyncStatus.Text = UiText.Get("WpfSyncCachePartial"); }
            finally { _syncing = false; SetInterfaceBusy(false); }
        }

        private void SetInterfaceBusy(bool busy)
        {
            if (_view.OwnerWindow.Content is UIElement content) content.IsEnabled = !busy;
            _view.OwnerWindow.Cursor = busy ? Cursors.Wait : null;
        }

        public async void ActivateScanner()
        {
            if (!_scanInitialized)
            {
                _scanInitialized = true;
                await _viewModel.InitializeAsync();
            }
            FocusScannerInput();
        }

        private void MainWindow_Activated(object sender, EventArgs e)
        {
            if (_view.IsScanViewActive) FocusScannerInput();
        }

        public void ScanTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            InputMethod.SetPreferredImeState(_view.SnTextBox, InputMethodState.Off);
        }

        private void ViewModel_FocusRequested(object sender, EventArgs e)
        {
            FocusScannerInput();
        }

        private void FocusScannerInput()
        {
            if (!_view.IsScanViewActive) return;
            _view.SnTextBox.Focus();
            Keyboard.Focus(_view.SnTextBox);
            _view.SnTextBox.SelectAll();
        }

        public void ImportGlobalBase_Click(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = UiText.Get("SelectBaseFileTitle"), Filter = UiText.Get("ExcelFileFilter"), CheckFileExists = true };
            if (dialog.ShowDialog(_view.OwnerWindow) != true) return;
            try
            {
                _baseData.ImportBase(dialog.FileName);
                MessageBox.Show(_view.OwnerWindow, string.Format(UiText.Get("BaseCacheSummary"), _baseData.Records.Count, _baseData.BaseDataImportedAt), UiText.Get("GlobalBaseImport"));
            }
            catch (Exception ex) { MessageBox.Show(_view.OwnerWindow, UiText.Get("BaseImportFailedPrefix") + ex.Message); }
        }

        private void ReportSync(BluettiSyncResult result)
        {
            var counts = string.Format(UiText.Get("WpfSyncCounts"), result.ForecastCount, result.BaseCount);
            string key = result.State switch
            {
                BluettiSyncState.ConfigurationProblem => "WpfConfigRequired",
                BluettiSyncState.Succeeded => "WpfSyncSucceeded",
                BluettiSyncState.Failed => "WpfSyncFailed",
                _ => "WpfSyncImporting"
            };
            if (result.TimedOutStage.HasValue)
                key = result.TimedOutStage.Value switch
                {
                    BluettiExportStage.PreparingPage => "WpfSyncPageTimeout",
                    BluettiExportStage.GeneratingFile => "WpfSyncGenerationTimeout",
                    _ => "WpfSyncDownloadTimeout"
                };
            _view.AutoSyncStatus.Text = UiText.Get(key) +
                (result.State == BluettiSyncState.ConfigurationProblem && !result.ForecastImported && !result.BaseImported ? "" : Environment.NewLine + counts) +
                (result.ErrorCode != null ? Environment.NewLine + string.Format(UiText.Get("WpfSyncErrorCode"), result.ErrorCode) : "") +
                (result.CleanupPending ? Environment.NewLine + UiText.Get("WpfSyncCleanupPending") : "");
            _view.AutoSyncButton.Tag = result.State;
        }

        public async void OpenAutoSync()
        {
            if (_syncing) return;
            _syncing = true;
            SetInterfaceBusy(true);
            _view.AutoSyncButton.Tag = BluettiSyncState.CheckingConfiguration;
            _view.AutoSyncStatus.Text = UiText.Get("WpfSyncChecking");
            try
            {
                var sync = new BluettiSyncService();
                var session = await sync.PrepareAsync(_syncCancellation.Token);
                var exporter = new BluettiApiExporter();
                await sync.ImportAsync(session, exporter.ExportAsync, _viewModel.ApplyForecastFile, _baseData,
                    ReportSync, _syncCancellation.Token);
            }
            catch (BluettiConfigurationException) { ReportSync(new BluettiSyncResult { State = BluettiSyncState.ConfigurationProblem }); }
            catch (OperationCanceledException) { if (!_syncCancellation.IsCancellationRequested) ReportSync(new BluettiSyncResult { State = BluettiSyncState.Failed }); }
            catch (Exception) { ReportSync(new BluettiSyncResult { State = BluettiSyncState.Failed }); }
            finally
            {
                _syncing = false;
                SetInterfaceBusy(false);
            }
        }

        private bool RequireGlobalBase()
        {
            if (_baseData.Records.Count > 0) return true;
            MessageBox.Show(_view.OwnerWindow, UiText.Get("GlobalBaseRequired"));
            return false;
        }

        public void OpenRegistrationRepair_Click(object sender, RoutedEventArgs e)
        {
            new RegistrationRepairWindow(_baseData, _palletApi) { Owner = _view.OwnerWindow }.ShowDialog();
        }
        public void OpenBatchLabel_Click(object sender, RoutedEventArgs e)
        {
            new BatchLabelWindow { Owner = _view.OwnerWindow }.ShowDialog();
        }

        public void OpenGoodArea_Click(object sender, RoutedEventArgs e)
        {
            new GoodAreaWindow(_palletApi) { Owner = _view.OwnerWindow }.ShowDialog();
        }

        public void OpenAutomaticMatching_Click(object sender, RoutedEventArgs e)
        {
            if (!RequireGlobalBase()) return;
            new AutomaticMatchingWindow(_baseData, _palletApi) { Owner = _view.OwnerWindow }.ShowDialog();
        }

        public void OpenMonthlyReport_Click(object sender, RoutedEventArgs e)
        {
            if (_baseData.MonthlySource == null)
            {
                MessageBox.Show(_view.OwnerWindow, Scanner.WPF.Helpers.UiText.Get("WpfComplete232"), Scanner.WPF.Helpers.UiText.Get("WpfComplete046"));
                return;
            }
            new MonthlyReportWindow(_baseData.MonthlySource) { Owner = _view.OwnerWindow }.ShowDialog();
        }

        public void OpenChecklistButton_Click(object sender, RoutedEventArgs e)
        {
            if (!RequireGlobalBase()) return;
            _windows.ShowChecklist(_view.OwnerWindow);
            FocusScannerInput();
        }

        public void OpenOutboundInspectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!RequireGlobalBase()) return;
            _windows.ShowOutbound(_view.OwnerWindow);
            FocusScannerInput();
        }

        public void OpenLocationFeeComparisonButton_Click(object sender, RoutedEventArgs e)
        {
            if (!RequireGlobalBase()) return;
            _windows.ShowLocationFee(_view.OwnerWindow);
            FocusScannerInput();
        }

        public void CreateDailyReportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = DailyReportService.Create(DateTime.Now);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(_view.OwnerWindow, UiText.Get("DailyReportCreateFailedPrefix") + ex.Message,
                    UiText.Get("DailyReport"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            FocusScannerInput();
        }


        public override void Dispose() { _syncCancellation.Cancel(); _viewModel.FocusRequested -= ViewModel_FocusRequested; _view.OwnerWindow.Activated -= MainWindow_Activated; }

    }
}
