using Scanner.Helpers;
using Microsoft.Win32;
using Scanner.Services;
using System;
using System.Diagnostics;
using System.Windows;

using Scanner.Controllers;
namespace Scanner.PlatformControllers
{
    public sealed class LocationFeeComparisonWindowController : ControllerBase
    {
        private readonly ILocationFeeComparisonWindowView _view;

        private string _templatePath;

        private readonly ChecklistDataCache _baseData;
        public LocationFeeComparisonWindowController(ILocationFeeComparisonWindowView view, ChecklistDataCache baseData)
        {
            _view = view;

            _baseData = baseData ?? throw new ArgumentNullException(nameof(baseData));
            _view.BaseFileTextBlock.Text = _baseData.BaseDataFile ?? UiText.Get("GlobalBaseRequired");
            RefreshStatus();
        
        }

        public void SelectTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Title = UiText.Get("SelectFeeTemplateTitle"), Filter = UiText.Get("ExcelFileFilter"), CheckFileExists = true };
            if (dialog.ShowDialog(_view.OwnerWindow) != true) return;
            _templatePath = dialog.FileName;
            _view.TemplateFileTextBlock.Text = _templatePath;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            _view.ExportButton.IsEnabled = !string.IsNullOrWhiteSpace(_templatePath) && _baseData.Records.Count > 0;
            _view.StatusTextBlock.Text = _view.ExportButton.IsEnabled ? UiText.Get("FilesReady") : UiText.Get("SelectBothFiles");
        }

        public void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = UiText.Get("SaveFeeTitle"),
                Filter = UiText.Get("ExcelFileFilter"),
                DefaultExt = ".xlsx",
                AddExtension = true,
                FileName = "库位付费比对结果_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx"
            };
            if (dialog.ShowDialog(_view.OwnerWindow) != true) return;
            try
            {
                _view.ExportButton.IsEnabled = false;
                _view.StatusTextBlock.Text = UiText.Get("ProcessingSheets");
                LocationFeeComparisonSummary summary = LocationFeeComparisonService.Build(_templatePath, _baseData.Records, dialog.FileName);
                _view.StatusTextBlock.Text = string.Format(UiText.Get("FeeSummary"), summary.SheetCount, summary.RowCount, summary.MatchedCount, summary.UnmatchedCount);
                MessageBox.Show(_view.OwnerWindow, _view.StatusTextBlock.Text + UiText.Get("SavedFilePrefix") + dialog.FileName, UiText.Get("LocationFeeTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _view.StatusTextBlock.Text = UiText.Get("ProcessingFailed");
                MessageBox.Show(_view.OwnerWindow, UiText.Get("FeeFailedPrefix") + ex.Message, UiText.Get("LocationFeeTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _view.ExportButton.IsEnabled = !string.IsNullOrWhiteSpace(_templatePath) && _baseData.Records.Count > 0;
            }
        }
    
    }
}
