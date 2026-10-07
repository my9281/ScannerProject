using Microsoft.Win32;
using Scanner.Helpers.Services;
using Scanner.WPF.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Scanner.WPF
{
    public partial class BatchLabelWindow : Window
    {
        private readonly PrintingHelper _printing = new PrintingHelper();
        private IReadOnlyList<BatchLabelRecord> _rows;
        private bool _busy, _cancel;
        public BatchLabelWindow()
        {
            InitializeComponent();
            Closing += (sender, e) => { if (_busy) e.Cancel = true; };
        }
        private void SetBusy(bool busy)
        {
            _busy = busy;
            ImportButton.IsEnabled = !busy;
            PrintButton.IsEnabled = !busy && _rows != null && _rows.Any(r => r.Error == null && r.SentCount < r.Quantity);
        }
        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Title = "导入批量标签 Excel", Filter = "Excel 工作簿 (*.xlsx)|*.xlsx", CheckFileExists = true };
            if (dialog.ShowDialog(this) != true) return;
            SetBusy(true);
            try
            {
                var rows = await Task.Run(() => BatchLabelImportService.Load(dialog.FileName));
                _rows = rows;
                FileText.Text = Path.GetFileName(dialog.FileName);
                RowsGrid.ItemsSource = rows;
                RowsGrid.SelectedItem = rows.FirstOrDefault(r => r.Error == null);
                long total = rows.Where(r => r.Error == null).Sum(r => (long)r.Quantity);
                PrintProgress.Maximum = Math.Max(1, total); PrintProgress.Value = 0;
                StatusText.Text = "已导入 " + rows.Count + " 行，可打印 " + total + " 张，报废 " + rows.Where(r => r.Error == null && r.IsScrapped).Sum(r => (long)r.Quantity) + " 张，跳过 " + rows.Count(r => r.Error != null) + " 行。";
            }
            catch (Exception ex) { StatusText.Text = "导入失败：" + ex.Message; }
            finally { SetBusy(false); }
        }
        private void Selection_Changed(object sender, SelectionChangedEventArgs e)
        {
            PreviewImage.Source = null;
            if (!(RowsGrid.SelectedItem is BatchLabelRecord row)) { PreviewStatus.Text = "请选择一行。"; return; }
            if (row.Error != null) { PreviewStatus.Text = row.Error; return; }
            try
            {
                PreviewImage.Source = PrintingHelper.PreviewSkuSerialNumberLabel(row.Sku, row.Sn, row.Remark, row.IsScrapped);
                PreviewStatus.Text = row.IsScrapped ? "报废 / Scrapped / Desechado" : "备注：" + row.Remark;
            }
            catch (Exception ex) { PreviewStatus.Text = "预览失败：" + ex.Message; }
        }
        private void Cancel_Click(object sender, RoutedEventArgs e) { _cancel = true; CancelButton.IsEnabled = false; }
        private async void Print_Click(object sender, RoutedEventArgs e)
        {
            if (_rows == null) return;
            SetBusy(true); _cancel = false; CancelButton.IsEnabled = true;
            BatchLabelRecord current = null;
            try
            {
                string printer = _printing.GetDefaultPrinterName();
                long total = _rows.Where(r => r.Error == null).Sum(r => (long)r.Quantity);
                long sent = _rows.Sum(r => (long)r.SentCount);
                foreach (var row in _rows.Where(r => r.Error == null))
                {
                    current = row;
                    while (row.SentCount < row.Quantity)
                    {
                        await Dispatcher.Yield(DispatcherPriority.Background);
                        if (_cancel) break;
                        _printing.PrintSkuSerialNumberLabel(row.Sku, row.Sn, row.Remark, row.IsScrapped);
                        row.SentCount++; sent++;
                        row.PrintStatus = "已发送 " + row.SentCount + "/" + row.Quantity;
                        PrintProgress.Value = sent;
                        RowsGrid.Items.Refresh();
                        StatusText.Text = "已发送 " + sent + "/" + total + " 张到 " + printer + "。";
                    }
                    if (_cancel) break;
                }
                StatusText.Text = (_cancel ? "已停止后续打印。" : "批量标签已发送。") + "已发送 " + sent + "/" + total + " 张到 " + printer + "。";
            }
            catch (Exception ex)
            {
                if (current != null) current.PrintStatus = "失败（已发送 " + current.SentCount + " 张）：" + ex.Message;
                RowsGrid.Items.Refresh();
                StatusText.Text = "打印已停止：" + ex.Message + "。请核对打印队列后再继续。";
            }
            finally { CancelButton.IsEnabled = false; SetBusy(false); }
        }
    }
}
