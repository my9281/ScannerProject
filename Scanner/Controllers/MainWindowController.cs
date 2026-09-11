using Scanner.Helpers;
using Scanner.Services;
using Scanner.ViewModels;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

using Scanner.Controllers;
namespace Scanner.PlatformControllers
{
    public sealed class MainWindowController : ControllerBase
    {
        private readonly IMainWindowView _view;
        private readonly IDesktopWindows _windows;

        private readonly MainWindowViewModel _viewModel;
        private readonly ChecklistDataCache _baseData;
        public MainWindowController(IMainWindowView view, ChecklistDataCache baseData, MainWindowViewModel viewModel, IDesktopWindows windows)
        {
            _view = view;
            _windows = windows;

            _baseData = baseData ?? throw new ArgumentNullException(nameof(baseData));
            _viewModel = viewModel;
            _view.OwnerWindow.DataContext = _viewModel;
            _viewModel.FocusRequested += ViewModel_FocusRequested;
            _view.OwnerWindow.Activated += MainWindow_Activated;
        
        }

        public async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
        }

        private void MainWindow_Activated(object sender, EventArgs e)
        {
            FocusScannerInput();
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
            _view.SnTextBox.Focus();
            Keyboard.Focus(_view.SnTextBox);
            _view.SnTextBox.SelectAll();
        }

        public void ImportGlobalBase_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = UiText.Get("SelectBaseFileTitle"), Filter = UiText.Get("ExcelFileFilter"), CheckFileExists = true };
            if (dialog.ShowDialog(_view.OwnerWindow) != true) return;
            try
            {
                _baseData.ImportBase(dialog.FileName);
                _view.GlobalBaseButton.ToolTip = _baseData.BaseDataFile;
                MessageBox.Show(_view.OwnerWindow, string.Format(UiText.Get("BaseCacheSummary"), _baseData.Records.Count, _baseData.BaseDataImportedAt), UiText.Get("GlobalBaseImport"));
            }
            catch (Exception ex) { MessageBox.Show(_view.OwnerWindow, UiText.Get("BaseImportFailedPrefix") + ex.Message); }
        }

        private bool RequireGlobalBase()
        {
            if (_baseData.Records.Count > 0) return true;
            MessageBox.Show(_view.OwnerWindow, UiText.Get("GlobalBaseRequired"));
            return false;
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

    
        public override void Dispose() { _viewModel.FocusRequested -= ViewModel_FocusRequested; _view.OwnerWindow.Activated -= MainWindow_Activated; }

    }
}
