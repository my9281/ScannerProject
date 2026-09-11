using Scanner.Helpers;
using Scanner.Services;
using Scanner.ViewModels;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace Scanner
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;
        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainWindowViewModel();
            DataContext = _viewModel;
            _viewModel.FocusRequested += ViewModel_FocusRequested;
            Activated += MainWindow_Activated;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
        }

        private void MainWindow_Activated(object sender, EventArgs e)
        {
            FocusScannerInput();
        }

        private void ScanTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            InputMethod.SetPreferredImeState(SnTextBox, InputMethodState.Off);
        }

        private void ViewModel_FocusRequested(object sender, EventArgs e)
        {
            FocusScannerInput();
        }

        private void FocusScannerInput()
        {
            SnTextBox.Focus();
            Keyboard.Focus(SnTextBox);
            SnTextBox.SelectAll();
        }

        private void ImportGlobalBase_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = UiText.Get("SelectBaseFileTitle"), Filter = UiText.Get("ExcelFileFilter"), CheckFileExists = true };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                ChecklistDataCache.ImportBase(dialog.FileName);
                GlobalBaseButton.ToolTip = ChecklistDataCache.BaseDataFile;
                MessageBox.Show(this, string.Format(UiText.Get("BaseCacheSummary"), ChecklistDataCache.Records.Count, ChecklistDataCache.BaseDataImportedAt), UiText.Get("GlobalBaseImport"));
            }
            catch (Exception ex) { MessageBox.Show(this, UiText.Get("BaseImportFailedPrefix") + ex.Message); }
        }

        private bool RequireGlobalBase()
        {
            if (ChecklistDataCache.Records.Count > 0) return true;
            MessageBox.Show(this, UiText.Get("GlobalBaseRequired"));
            return false;
        }

        private void OpenChecklistButton_Click(object sender, RoutedEventArgs e)
        {
            if (!RequireGlobalBase()) return;
            ChecklistWindow window = new ChecklistWindow { Owner = this };
            window.ShowDialog();
            FocusScannerInput();
        }

        private void OpenOutboundInspectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!RequireGlobalBase()) return;
            OutboundInspectionWindow window = new OutboundInspectionWindow { Owner = this };
            window.ShowDialog();
            FocusScannerInput();
        }

        private void OpenLocationFeeComparisonButton_Click(object sender, RoutedEventArgs e)
        {
            if (!RequireGlobalBase()) return;
            LocationFeeComparisonWindow window = new LocationFeeComparisonWindow { Owner = this };
            window.ShowDialog();
            FocusScannerInput();
        }

        private void CreateDailyReportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = DailyReportService.Create(DateTime.Now);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, UiText.Get("DailyReportCreateFailedPrefix") + ex.Message,
                    UiText.Get("DailyReport"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            FocusScannerInput();
        }

    }
}
