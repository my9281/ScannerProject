using Microsoft.Win32;
using Scanner.Controllers;
using Scanner.Helpers.Services;
using Scanner.Models;
using Scanner.WPF.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
namespace Scanner.WPF.Controllers
{
    public sealed class OutboundInspectionWindowController : ControllerBase
    {
        private readonly IOutboundInspectionWindowView _view;

        private readonly PrintingHelper _printing;
        private IList<OutboundSkuSummary> _skuItems = new List<OutboundSkuSummary>();
        private string _textPath;
        private IList<OutboundInspectionRecord> _records = new List<OutboundInspectionRecord>();

        private readonly ChecklistDataCache _baseData;
        public OutboundInspectionWindowController(IOutboundInspectionWindowView view, ChecklistDataCache baseData, PrintingHelper printing)
        {
            _view = view;

            _baseData = baseData ?? throw new ArgumentNullException(nameof(baseData));
            _view.BaseFileTextBlock.Text = _baseData.BaseDataFile ?? UiText.Get("GlobalBaseRequired");
            _view.OwnerWindow.Loaded += (sender, args) => _view.PalletNumberTextBox.Focus();

            _printing = printing;
        }

        public void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            string palletNumber = (_view.PalletNumberTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(palletNumber))
            {
                MessageBox.Show(_view.OwnerWindow, UiText.Get("PalletRequired"), UiText.Get("OutboundTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                _view.PalletNumberTextBox.Focus();
                return;
            }
            try
            {
                _printing.PrintOutboundInspection(palletNumber, _skuItems);
                MessageBox.Show(_view.OwnerWindow, UiText.Get("SkuPrintSent"), UiText.Get("PrintComplete"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(_view.OwnerWindow, UiText.Get("PrintFailedPrefix") + ex.Message, UiText.Get("OutboundTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void SelectTextButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Title = UiText.Get("SelectSkuSnTitle"), Filter = UiText.Get("TextFileFilter"), CheckFileExists = true };
            if (dialog.ShowDialog(_view.OwnerWindow) != true) return;
            _textPath = dialog.FileName;
            _view.TextFileTextBlock.Text = _textPath;
            TryLoad();
        }

        private void TryLoad()
        {
            if (string.IsNullOrWhiteSpace(_textPath) || _baseData.Records.Count == 0) return;
            try
            {
                _records = OutboundInspectionService.Build(_textPath, _baseData.Records);
                _skuItems = _records.Where(item => !string.IsNullOrWhiteSpace(item.Sku)).GroupBy(item => item.Sku.Trim(), StringComparer.OrdinalIgnoreCase).Select(group => new OutboundSkuSummary { Sku = group.Key, Quantity = group.Count() }).OrderBy(item => item.Sku, StringComparer.OrdinalIgnoreCase).ToList();
                _view.ResultDataGrid.ItemsSource = _records;
                int matched = _records.Count(item => item.IsMatched);
                _view.SummaryTextBlock.Text = string.Format(UiText.Get("OutboundSummary"), _records.Count, _skuItems.Count, matched, _records.Count - matched);
                _view.ExportButton.IsEnabled = _records.Count > 0;
                _view.PrintButton.IsEnabled = _skuItems.Count > 0;
            }
            catch (Exception ex)
            {
                _records = new List<OutboundInspectionRecord>();
                _view.ResultDataGrid.ItemsSource = null;
                _view.ExportButton.IsEnabled = false;
                _view.PrintButton.IsEnabled = false;
                _skuItems = new List<OutboundSkuSummary>();
                _view.SummaryTextBlock.Text = UiText.Get("ReadFailed");
                MessageBox.Show(_view.OwnerWindow, UiText.Get("OutboundFailedPrefix") + ex.Message, UiText.Get("OutboundTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog { Title = UiText.Get("ExportOutboundTitle"), Filter = UiText.Get("ExcelFileFilter"), DefaultExt = ".xlsx", AddExtension = true, FileName = "出库检测结果_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx" };
            if (dialog.ShowDialog(_view.OwnerWindow) != true) return;
            try
            {
                OutboundInspectionXlsxWriter.Write(dialog.FileName, _records);
                MessageBox.Show(_view.OwnerWindow, UiText.Get("ExportedPrefix") + dialog.FileName, UiText.Get("ExportComplete"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(_view.OwnerWindow, UiText.Get("ExportFailedPrefix") + ex.Message, UiText.Get("OutboundTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}
