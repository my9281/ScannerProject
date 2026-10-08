using Microsoft.Win32;
using Scanner.Helpers.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace Scanner.WPF
{
    public partial class AutomaticMatchingWindow : Window
    {
        private readonly ChecklistDataCache _baseData;
        private readonly ShelvedPalletApiService _api;
        private IReadOnlyList<AutomaticMatchRow> _rows;
        public AutomaticMatchingWindow(ChecklistDataCache baseData, ShelvedPalletApiService api)
        {
            InitializeComponent();
            _baseData = baseData;
            _api = api;
        }
        private async void Fetch_Click(object sender, RoutedEventArgs e)
        {
            FetchButton.IsEnabled = ExportButton.IsEnabled = UploadButton.IsEnabled = false;
            _rows = null;
            ResultsGrid.ItemsSource = null;
            StatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete112");
            try
            {
                var inbound = _baseData.Records.ToArray();
                var pallets = await _api.GetPdaPalletsAsync(System.Configuration.ConfigurationManager.AppSettings["UploadApiKey"]);
                _rows = AutomaticMatchingService.Match(pallets, inbound);
                ResultsGrid.ItemsSource = _rows;
                StatusText.Text = string.Format(Scanner.WPF.Helpers.UiText.Get("WpfComplete113"), pallets.Count, _rows.Count, _rows.Select(x => x.PalletNumber).Distinct().Count());
                ExportButton.IsEnabled = _rows.Count > 0;
                if (_rows.Count > 0) await UploadRowsAsync();
            }
            catch (Exception ex) { StatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete114") + ex.Message; }
            finally { FetchButton.IsEnabled = true; }
        }
        private async System.Threading.Tasks.Task UploadRowsAsync()
        {
            StatusText.Text = string.Format(Scanner.WPF.Helpers.UiText.Get("WpfComplete115"), _rows.Count);
            UploadButton.IsEnabled = false;
            try
            {
                var result = await _api.UploadMatchedWorkOrdersAsync(_rows, System.Configuration.ConfigurationManager.AppSettings["UploadApiKey"]);
                StatusText.Text = string.Format(Scanner.WPF.Helpers.UiText.Get("WpfComplete116"), _rows.Count, result.InsertedCount, result.SkippedCount, result.Username);
            }
            catch (Exception ex)
            {
                StatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete117") + ex.Message;
                UploadButton.IsEnabled = true;
            }
        }
        private async void Upload_Click(object sender, RoutedEventArgs e)
        {
            if (_rows == null || _rows.Count == 0) return;
            FetchButton.IsEnabled = false;
            try { await UploadRowsAsync(); }
            finally { FetchButton.IsEnabled = true; }
        }
        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (_rows == null || _rows.Count == 0) return;
            var dialog = new SaveFileDialog { Filter = Scanner.WPF.Helpers.UiText.Get("ExcelFileFilter"), FileName = Scanner.WPF.Helpers.UiText.Get("WpfComplete118") + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx" };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                if (string.Equals(Path.GetFullPath(dialog.FileName), Path.GetFullPath(_baseData.BaseDataFile), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(Scanner.WPF.Helpers.UiText.Get("WpfComplete119"));
                AutomaticMatchingService.Export(dialog.FileName, _rows);
                StatusText.Text = Scanner.WPF.Helpers.UiText.Get("ExportedPrefix") + dialog.FileName;
            }
            catch (Exception ex) { MessageBox.Show(this, Scanner.WPF.Helpers.UiText.Get("ExportFailedPrefix") + ex.Message); }
        }
    }
}
