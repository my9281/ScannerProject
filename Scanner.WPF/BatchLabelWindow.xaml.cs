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
            var dialog = new OpenFileDialog { Title = Scanner.WPF.Helpers.UiText.Get("WpfComplete120"), Filter = Scanner.WPF.Helpers.UiText.Get("ExcelFileFilter"), CheckFileExists = true };
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
                StatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete121") + rows.Count + Scanner.WPF.Helpers.UiText.Get("WpfComplete122") + total + Scanner.WPF.Helpers.UiText.Get("WpfComplete123") + rows.Where(r => r.Error == null && r.IsScrapped).Sum(r => (long)r.Quantity) + Scanner.WPF.Helpers.UiText.Get("WpfComplete124") + rows.Count(r => r.Error != null) + Scanner.WPF.Helpers.UiText.Get("WpfComplete125");
            }
            catch (Exception ex) { StatusText.Text = Scanner.WPF.Helpers.UiText.Get("ImportFailedPrefix") + ex.Message; }
            finally { SetBusy(false); }
        }
        private void Selection_Changed(object sender, SelectionChangedEventArgs e)
        {
            PreviewImage.Source = null;
            if (!(RowsGrid.SelectedItem is BatchLabelRecord row)) { PreviewStatus.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete126"); return; }
            if (row.Error != null) { PreviewStatus.Text = row.Error; return; }
            try
            {
                PreviewImage.Source = PrintingHelper.PreviewSkuSerialNumberLabel(row.Sku, row.Sn, row.Remark, row.IsScrapped);
                PreviewStatus.Text = row.IsScrapped ? Scanner.WPF.Helpers.UiText.Get("WpfComplete127") : Scanner.WPF.Helpers.UiText.Get("WpfComplete128") + row.Remark;
            }
            catch (Exception ex) { PreviewStatus.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete129") + ex.Message; }
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
                        row.PrintStatus = Scanner.WPF.Helpers.UiText.Get("WpfComplete130") + row.SentCount + "/" + row.Quantity;
                        PrintProgress.Value = sent;
                        RowsGrid.Items.Refresh();
                        StatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete130") + sent + "/" + total + Scanner.WPF.Helpers.UiText.Get("WpfComplete131") + printer + "。";
                    }
                    if (_cancel) break;
                }
                StatusText.Text = (_cancel ? Scanner.WPF.Helpers.UiText.Get("WpfComplete132") : Scanner.WPF.Helpers.UiText.Get("WpfComplete133")) + Scanner.WPF.Helpers.UiText.Get("WpfComplete130") + sent + "/" + total + Scanner.WPF.Helpers.UiText.Get("WpfComplete131") + printer + "。";
            }
            catch (Exception ex)
            {
                if (current != null) current.PrintStatus = Scanner.WPF.Helpers.UiText.Get("WpfComplete134") + current.SentCount + Scanner.WPF.Helpers.UiText.Get("WpfComplete135") + ex.Message;
                RowsGrid.Items.Refresh();
                StatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete136") + ex.Message + Scanner.WPF.Helpers.UiText.Get("WpfComplete137");
            }
            finally { CancelButton.IsEnabled = false; SetBusy(false); }
        }
    }
}
