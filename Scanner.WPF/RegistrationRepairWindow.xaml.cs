using Microsoft.Win32;
using Scanner.Helpers.Services;
using System;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace Scanner.WPF
{
    public partial class RegistrationRepairWindow : Window
    {
        private RegistrationRepairResult _result;
        private string _sourcePath;
        private bool _busy;
        private readonly ChecklistDataCache _baseData;
        private readonly ShelvedPalletApiService _palletApi;
        private IReadOnlyList<WarehouseInventoryRow> _inventory;
        private string _inventoryPath;
        public RegistrationRepairWindow(ChecklistDataCache baseData, ShelvedPalletApiService palletApi)
        {
            InitializeComponent();
            _baseData = baseData ?? throw new ArgumentNullException(nameof(baseData));
            _palletApi = palletApi ?? throw new ArgumentNullException(nameof(palletApi));
            var years = baseData.MonthlySource?.Years ?? new int[0];
            YearBox.ItemsSource = years.Concat(new[] { DateTime.Today.Year }).Distinct().OrderByDescending(y => y).ToList();
            YearBox.SelectedItem = years.Count > 0 ? years.Max() : DateTime.Today.Year;
            MonthBox.ItemsSource = Enumerable.Range(1, 12).ToList();
            MonthBox.SelectedItem = DateTime.Today.Month;
            var settlement = DateTime.Today.AddMonths(-1);
            RentYearBox.ItemsSource = YearBox.ItemsSource;
            RentYearBox.Text = settlement.Year.ToString(CultureInfo.InvariantCulture);
            RentMonthBox.ItemsSource = Enumerable.Range(1, 12).ToList();
            RentMonthBox.SelectedItem = settlement.Month;
            SnapshotDateBox.SelectedDate = DateTime.Today;
            BaseFileText.Text = baseData.MonthlySource == null ? Scanner.WPF.Helpers.UiText.Get("WpfComplete171") : Scanner.WPF.Helpers.UiText.Get("WpfComplete172") + Path.GetFileName(baseData.BaseDataFile);
            Closing += (sender, e) => { if (_busy) e.Cancel = true; };
        }
        private void SetBusy(bool busy)
        {
            _busy = busy;
            ImportButton.IsEnabled = !busy;
            ExportButton.IsEnabled = !busy && _result != null;
            WarehouseExportButton.IsEnabled = !busy && _result != null && _baseData.MonthlySource != null;
            YearBox.IsEnabled = MonthBox.IsEnabled = !busy;
            WebInventoryButton.IsEnabled = InventoryFileButton.IsEnabled = InventoryTemplateButton.IsEnabled = !busy;
            SnapshotDateBox.IsEnabled = RentYearBox.IsEnabled = RentMonthBox.IsEnabled = !busy;
            RentExportButton.IsEnabled = !busy && _inventory != null && _result != null && _baseData.MonthlySource != null;
        }
        private async void ImportWebInventory_Click(object sender, RoutedEventArgs e)
        {
            SetBusy(true); RentStatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete173");
            try
            {
                var rows = await _palletApi.GetWarehouseInventoryAsync(System.Configuration.ConfigurationManager.AppSettings["UploadApiKey"]);
                _inventory = rows; _inventoryPath = null;
                SnapshotDateBox.SelectedDate = DateTime.Today;
                InventoryText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete174") + rows.Count + Scanner.WPF.Helpers.UiText.Get("WpfComplete175") + DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                RentStatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete176");
            }
            catch (Exception ex) { RentStatusText.Text = Scanner.WPF.Helpers.UiText.Get("ImportFailedPrefix") + ex.Message; }
            finally { SetBusy(false); }
        }
        private async void ImportInventoryFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Title = Scanner.WPF.Helpers.UiText.Get("WpfComplete177"), Filter = Scanner.WPF.Helpers.UiText.Get("ExcelFileFilter") };
            if (dialog.ShowDialog(this) != true) return;
            SetBusy(true); RentStatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete178");
            try
            {
                var rows = await Task.Run(() => WarehouseRentService.Import(dialog.FileName));
                _inventory = rows; _inventoryPath = dialog.FileName;
                InventoryText.Text = Path.GetFileName(dialog.FileName) + "：" + rows.Count + Scanner.WPF.Helpers.UiText.Get("WpfComplete179");
                RentStatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete180");
            }
            catch (Exception ex) { RentStatusText.Text = Scanner.WPF.Helpers.UiText.Get("ImportFailedPrefix") + ex.Message; }
            finally { SetBusy(false); }
        }
        private void InventoryTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog { Filter = Scanner.WPF.Helpers.UiText.Get("ExcelFileFilter"), FileName = Scanner.WPF.Helpers.UiText.Get("WpfComplete181") };
            if (dialog.ShowDialog(this) != true) return;
            if (IsSourcePath(dialog.FileName)) { MessageBox.Show(this, Scanner.WPF.Helpers.UiText.Get("WpfComplete182")); return; }
            try { WarehouseRentService.ExportTemplate(dialog.FileName); RentStatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete183"); }
            catch (Exception ex) { RentStatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete184") + ex.Message; }
        }
        private bool IsSourcePath(string path)
        {
            return new[] { _sourcePath, _baseData.BaseDataFile, _inventoryPath }.Where(p => !string.IsNullOrWhiteSpace(p))
                .Any(p => string.Equals(Path.GetFullPath(path), Path.GetFullPath(p), StringComparison.OrdinalIgnoreCase));
        }
        private async void RentExport_Click(object sender, RoutedEventArgs e)
        {
            int year;
            if (!int.TryParse(RentYearBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out year) || year < 1 || year > 9999 || !(RentMonthBox.SelectedItem is int) || !SnapshotDateBox.SelectedDate.HasValue)
            { MessageBox.Show(this, Scanner.WPF.Helpers.UiText.Get("WpfComplete185"), Scanner.WPF.Helpers.UiText.Get("WpfComplete186")); return; }
            int month = (int)RentMonthBox.SelectedItem;
            DateTime snapshot = SnapshotDateBox.SelectedDate.Value;
            if (snapshot.Date <= new DateTime(year, month, DateTime.DaysInMonth(year, month))) { MessageBox.Show(this, Scanner.WPF.Helpers.UiText.Get("WpfComplete187"), Scanner.WPF.Helpers.UiText.Get("WpfComplete186")); return; }
            var dialog = new SaveFileDialog { Filter = Scanner.WPF.Helpers.UiText.Get("ExcelFileFilter"), FileName = year + Scanner.WPF.Helpers.UiText.Get("WpfComplete157") + month + Scanner.WPF.Helpers.UiText.Get("WpfComplete188"), DefaultExt = ".xlsx", AddExtension = true };
            if (dialog.ShowDialog(this) != true) return;
            if (IsSourcePath(dialog.FileName)) { MessageBox.Show(this, Scanner.WPF.Helpers.UiText.Get("WpfComplete182")); return; }
            SetBusy(true); RentStatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete189");
            try
            {
                var result = await Task.Run(() =>
                {
                    var calculation = WarehouseRentService.Calculate(_result, _inventory, _baseData.MonthlySource, year, month, snapshot);
                    WarehouseRentService.Export(calculation, dialog.FileName); return calculation;
                });
                RentStatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete190") + result.Rows.Count + Scanner.WPF.Helpers.UiText.Get("WpfComplete191") + result.Rows.Count(r => r.MonthDays.HasValue) + Scanner.WPF.Helpers.UiText.Get("WpfComplete192") + result.Rows.Sum(r => (long)(r.MonthDays ?? 0)) + Scanner.WPF.Helpers.UiText.Get("WpfComplete193") + result.Rows.Sum(r => (long)(r.TotalDays ?? 0)) + Scanner.WPF.Helpers.UiText.Get("WpfComplete194") + result.Issues.Count + Scanner.WPF.Helpers.UiText.Get("WpfComplete195") + result.FutureReceiptCount + Scanner.WPF.Helpers.UiText.Get("WpfComplete166");
                MessageBox.Show(this, RentStatusText.Text + "\n" + dialog.FileName, Scanner.WPF.Helpers.UiText.Get("WpfComplete196"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { RentStatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete197") + ex.Message; }
            finally { SetBusy(false); }
        }
        private static string Summary(RegistrationRepairResult result)
        {
            string text = Scanner.WPF.Helpers.UiText.Get("WpfComplete198") + result.Rows.Count + Scanner.WPF.Helpers.UiText.Get("WpfComplete199") + result.FilledDateCount + Scanner.WPF.Helpers.UiText.Get("WpfComplete200") + result.RemovedItemCount + Scanner.WPF.Helpers.UiText.Get("WpfComplete166");
            if (result.MissingDateCount > 0) text += Scanner.WPF.Helpers.UiText.Get("WpfComplete201") + result.MissingDateCount + Scanner.WPF.Helpers.UiText.Get("WpfComplete166");
            if (result.InvalidSystemDateCount > 0) text += Scanner.WPF.Helpers.UiText.Get("WpfComplete202") + result.InvalidSystemDateCount + Scanner.WPF.Helpers.UiText.Get("WpfComplete203");
            return text;
        }
        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Title = Scanner.WPF.Helpers.UiText.Get("WpfComplete204"), Filter = Scanner.WPF.Helpers.UiText.Get("ExcelFileFilter"), CheckFileExists = true };
            if (dialog.ShowDialog(this) != true) return;
            SetBusy(true);
            StatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete205");
            try
            {
                var result = await Task.Run(() => RegistrationRepairService.Load(dialog.FileName));
                _result = result;
                _sourcePath = dialog.FileName;
                FileText.Text = Path.GetFileName(dialog.FileName);
                StatusText.Text = Summary(result);
            }
            catch (Exception ex)
            {
                StatusText.Text = Scanner.WPF.Helpers.UiText.Get("ImportFailedPrefix") + ex.Message;
                MessageBox.Show(this, StatusText.Text, Scanner.WPF.Helpers.UiText.Get("WpfComplete053"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { SetBusy(false); }
        }
        private async void WarehouseExport_Click(object sender, RoutedEventArgs e)
        {
            if (_result == null || _baseData.MonthlySource == null) return;
            int year;
            if (!int.TryParse(YearBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out year) || year < 1 || year > 9999 || !(MonthBox.SelectedItem is int))
            {
                MessageBox.Show(this, Scanner.WPF.Helpers.UiText.Get("WpfComplete206"), Scanner.WPF.Helpers.UiText.Get("WpfComplete207"));
                return;
            }
            int month = (int)MonthBox.SelectedItem;
            var dialog = new SaveFileDialog { Title = Scanner.WPF.Helpers.UiText.Get("WpfComplete082"), Filter = Scanner.WPF.Helpers.UiText.Get("ExcelFileFilter"), DefaultExt = ".xlsx", AddExtension = true, FileName = year + Scanner.WPF.Helpers.UiText.Get("WpfComplete157") + month + Scanner.WPF.Helpers.UiText.Get("WpfComplete208") };
            if (dialog.ShowDialog(this) != true) return;
            string output = Path.GetFullPath(dialog.FileName);
            if (string.Equals(output, Path.GetFullPath(_sourcePath), StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(_baseData.BaseDataFile) && string.Equals(output, Path.GetFullPath(_baseData.BaseDataFile), StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, Scanner.WPF.Helpers.UiText.Get("WpfComplete209"), Scanner.WPF.Helpers.UiText.Get("WpfComplete207"));
                return;
            }
            var source = _baseData.MonthlySource;
            SetBusy(true);
            StatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete210");
            try
            {
                var result = await Task.Run(() => RegistrationRepairService.ExportWarehouseInspection(_result, source, year, month, dialog.FileName));
                StatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete160") + result.Count + Scanner.WPF.Helpers.UiText.Get("WpfComplete211") + year + Scanner.WPF.Helpers.UiText.Get("WpfComplete157") + month + Scanner.WPF.Helpers.UiText.Get("WpfComplete212");
                if (result.UnmatchedCount > 0) StatusText.Text += Scanner.WPF.Helpers.UiText.Get("WpfComplete213") + result.UnmatchedCount + Scanner.WPF.Helpers.UiText.Get("WpfComplete166");
                if (result.OutsideMonthCount > 0) StatusText.Text += Scanner.WPF.Helpers.UiText.Get("WpfComplete214") + result.OutsideMonthCount + Scanner.WPF.Helpers.UiText.Get("WpfComplete166");
                if (result.InvalidDateCount > 0) StatusText.Text += Scanner.WPF.Helpers.UiText.Get("WpfComplete168") + result.InvalidDateCount + Scanner.WPF.Helpers.UiText.Get("WpfComplete215");
                if (result.Count == 0) StatusText.Text += Scanner.WPF.Helpers.UiText.Get("WpfComplete216");
                MessageBox.Show(this, StatusText.Text + "\n" + dialog.FileName, Scanner.WPF.Helpers.UiText.Get("WpfComplete217"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { StatusText.Text = Scanner.WPF.Helpers.UiText.Get("ExportFailedPrefix") + ex.Message; MessageBox.Show(this, StatusText.Text, Scanner.WPF.Helpers.UiText.Get("WpfComplete207"), MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { SetBusy(false); }
        }
        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            if (_result == null) return;
            var dialog = new SaveFileDialog { Title = Scanner.WPF.Helpers.UiText.Get("WpfComplete218"), Filter = Scanner.WPF.Helpers.UiText.Get("ExcelFileFilter"), DefaultExt = ".xlsx", AddExtension = true, FileName = Path.GetFileNameWithoutExtension(_sourcePath) + Scanner.WPF.Helpers.UiText.Get("WpfComplete219") };
            if (dialog.ShowDialog(this) != true) return;
            if (string.Equals(Path.GetFullPath(dialog.FileName), Path.GetFullPath(_sourcePath), StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, Scanner.WPF.Helpers.UiText.Get("WpfComplete220"), Scanner.WPF.Helpers.UiText.Get("WpfComplete053"));
                return;
            }
            SetBusy(true);
            StatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete159");
            try
            {
                await Task.Run(() => RegistrationRepairService.Export(_result, dialog.FileName));
                StatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete221") + Summary(_result);
                MessageBox.Show(this, StatusText.Text + "\n" + dialog.FileName, Scanner.WPF.Helpers.UiText.Get("ExportComplete"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { StatusText.Text = Scanner.WPF.Helpers.UiText.Get("ExportFailedPrefix") + ex.Message; MessageBox.Show(this, StatusText.Text, Scanner.WPF.Helpers.UiText.Get("WpfComplete053"), MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { SetBusy(false); }
        }
    }
}
