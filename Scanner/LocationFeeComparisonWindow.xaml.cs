using Scanner.Helpers;
using Microsoft.Win32;
using Scanner.Services;
using System;
using System.Diagnostics;
using System.Windows;

namespace Scanner
{
    public partial class LocationFeeComparisonWindow : Window
    {
        private string _templatePath;

        public LocationFeeComparisonWindow()
        {
            InitializeComponent();
            BaseFileTextBlock.Text = ChecklistDataCache.BaseDataFile ?? UiText.Get("GlobalBaseRequired");
            RefreshStatus();
        }

        private void SelectTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Title = UiText.Get("SelectFeeTemplateTitle"), Filter = UiText.Get("ExcelFileFilter"), CheckFileExists = true };
            if (dialog.ShowDialog(this) != true) return;
            _templatePath = dialog.FileName;
            TemplateFileTextBlock.Text = _templatePath;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            ExportButton.IsEnabled = !string.IsNullOrWhiteSpace(_templatePath) && ChecklistDataCache.Records.Count > 0;
            StatusTextBlock.Text = ExportButton.IsEnabled ? UiText.Get("FilesReady") : UiText.Get("SelectBothFiles");
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = UiText.Get("SaveFeeTitle"),
                Filter = UiText.Get("ExcelFileFilter"),
                DefaultExt = ".xlsx",
                AddExtension = true,
                FileName = "库位付费比对结果_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx"
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                ExportButton.IsEnabled = false;
                StatusTextBlock.Text = UiText.Get("ProcessingSheets");
                LocationFeeComparisonSummary summary = LocationFeeComparisonService.Build(_templatePath, ChecklistDataCache.Records, dialog.FileName);
                StatusTextBlock.Text = string.Format(UiText.Get("FeeSummary"), summary.SheetCount, summary.RowCount, summary.MatchedCount, summary.UnmatchedCount);
                MessageBox.Show(this, StatusTextBlock.Text + UiText.Get("SavedFilePrefix") + dialog.FileName, UiText.Get("LocationFeeTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = UiText.Get("ProcessingFailed");
                MessageBox.Show(this, UiText.Get("FeeFailedPrefix") + ex.Message, UiText.Get("LocationFeeTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ExportButton.IsEnabled = !string.IsNullOrWhiteSpace(_templatePath) && ChecklistDataCache.Records.Count > 0;
            }
        }
    }
}
