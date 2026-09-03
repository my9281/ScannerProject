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
        private string _baseWorkbookPath;
        private IList<OutboundInspectionRecord> _records = new List<OutboundInspectionRecord>();

        public OutboundInspectionWindow()
        {
            InitializeComponent();
            Loaded += (sender, args) => PalletNumberTextBox.Focus();
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            string palletNumber = (PalletNumberTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(palletNumber))
            {
                MessageBox.Show(this, "请输入托盘号。", "出库检测", MessageBoxButton.OK, MessageBoxImage.Warning);
                PalletNumberTextBox.Focus();
                return;
            }
            try
            {
                _printing.PrintOutboundInspection(palletNumber, _skuItems);
                MessageBox.Show(this, "SKU 核对汇总已发送到默认打印机。", "打印完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "打印失败：" + ex.Message, "出库检测", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectTextButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Title = "选择 SKU/SN 文本文件", Filter = "文本文件 (*.txt)|*.txt", CheckFileExists = true };
            if (dialog.ShowDialog(this) != true) return;
            _textPath = dialog.FileName;
            TextFileTextBlock.Text = _textPath;
            TryLoad();
        }

        private void SelectBaseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Title = "选择 InboundDetection 基础表", Filter = "Excel 工作簿 (*.xlsx)|*.xlsx", CheckFileExists = true };
            if (dialog.ShowDialog(this) != true) return;
            _baseWorkbookPath = dialog.FileName;
            BaseFileTextBlock.Text = _baseWorkbookPath;
            TryLoad();
        }

        private void TryLoad()
        {
            if (string.IsNullOrWhiteSpace(_textPath) || string.IsNullOrWhiteSpace(_baseWorkbookPath)) return;
            try
            {
                _records = OutboundInspectionService.Build(_textPath, _baseWorkbookPath);
                _skuItems = _records.Where(item => !string.IsNullOrWhiteSpace(item.Sku)).GroupBy(item => item.Sku.Trim(), StringComparer.OrdinalIgnoreCase).Select(group => new OutboundSkuSummary { Sku = group.Key, Quantity = group.Count() }).OrderBy(item => item.Sku, StringComparer.OrdinalIgnoreCase).ToList();
                ResultDataGrid.ItemsSource = _records;
                int matched = _records.Count(item => item.IsMatched);
                SummaryTextBlock.Text = string.Format("共 {0:N0} 件，{1:N0} 种 SKU；基础表匹配 {2:N0} 条，未匹配 {3:N0} 条", _records.Count, _skuItems.Count, matched, _records.Count - matched);
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
                SummaryTextBlock.Text = "读取失败";
                MessageBox.Show(this, "生成出库检测数据失败：" + ex.Message, "出库检测", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog { Title = "导出出库检测 Excel", Filter = "Excel 工作簿 (*.xlsx)|*.xlsx", DefaultExt = ".xlsx", AddExtension = true, FileName = "出库检测结果_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx" };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                OutboundInspectionXlsxWriter.Write(dialog.FileName, _records);
                MessageBox.Show(this, "已导出：" + dialog.FileName, "导出完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "导出失败：" + ex.Message, "出库检测", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
