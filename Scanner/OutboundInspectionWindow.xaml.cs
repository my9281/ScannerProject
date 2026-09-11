using Microsoft.Win32;
using Scanner.Helpers;
using Scanner.Models;
using Scanner.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Scanner
{
    public partial class OutboundInspectionWindow : Window
    {
        private readonly PrintingHelper _printing = new PrintingHelper();
        private IList<OutboundSkuSummary> _skuItems = new List<OutboundSkuSummary>();
        private string _textPath;
        private IList<OutboundInspectionRecord> _records = new List<OutboundInspectionRecord>();

        public OutboundInspectionWindow()
        {
            InitializeComponent();
            BaseFileTextBlock.Text = ChecklistDataCache.BaseDataFile ?? UiText.Get("GlobalBaseRequired");
            Loaded += (sender, args) => PalletNumberTextBox.Focus();
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            string palletNumber = (PalletNumberTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(palletNumber))
            {
                MessageBox.Show(this, UiText.Get("PalletRequired"), UiText.Get("OutboundTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                PalletNumberTextBox.Focus();
                return;
            }
            try
            {
                _printing.PrintOutboundInspection(palletNumber, _skuItems);
                MessageBox.Show(this, UiText.Get("SkuPrintSent"), UiText.Get("PrintComplete"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, UiText.Get("PrintFailedPrefix") + ex.Message, UiText.Get("OutboundTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectTextButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Title = UiText.Get("SelectSkuSnTitle"), Filter = UiText.Get("TextFileFilter"), CheckFileExists = true };
            if (dialog.ShowDialog(this) != true) return;
            _textPath = dialog.FileName;
            TextFileTextBlock.Text = _textPath;
            TryLoad();
        }

        private void TryLoad()
        {
            if (string.IsNullOrWhiteSpace(_textPath) || ChecklistDataCache.Records.Count == 0) return;
            try
            {
                _records = OutboundInspectionService.Build(_textPath, ChecklistDataCache.Records);
                _skuItems = _records.Where(item => !string.IsNullOrWhiteSpace(item.Sku)).GroupBy(item => item.Sku.Trim(), StringComparer.OrdinalIgnoreCase).Select(group => new OutboundSkuSummary { Sku = group.Key, Quantity = group.Count() }).OrderBy(item => item.Sku, StringComparer.OrdinalIgnoreCase).ToList();
                ResultDataGrid.ItemsSource = _records;
                int matched = _records.Count(item => item.IsMatched);
                SummaryTextBlock.Text = string.Format(UiText.Get("OutboundSummary"), _records.Count, _skuItems.Count, matched, _records.Count - matched);
                ExportButton.IsEnabled = _records.Count > 0;
                PrintButton.IsEnabled = _skuItems.Count > 0;
            }
            catch (Exception ex)
            {
                _records = new List<OutboundInspectionRecord>();
                ResultDataGrid.ItemsSource = null;
                ExportButton.IsEnabled = false;
                PrintButton.IsEnabled = false;
                _skuItems = new List<OutboundSkuSummary>();
                SummaryTextBlock.Text = UiText.Get("ReadFailed");
                MessageBox.Show(this, UiText.Get("OutboundFailedPrefix") + ex.Message, UiText.Get("OutboundTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog { Title = UiText.Get("ExportOutboundTitle"), Filter = UiText.Get("ExcelFileFilter"), DefaultExt = ".xlsx", AddExtension = true, FileName = "出库检测结果_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx" };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                OutboundInspectionXlsxWriter.Write(dialog.FileName, _records);
                MessageBox.Show(this, UiText.Get("ExportedPrefix") + dialog.FileName, UiText.Get("ExportComplete"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, UiText.Get("ExportFailedPrefix") + ex.Message, UiText.Get("OutboundTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
