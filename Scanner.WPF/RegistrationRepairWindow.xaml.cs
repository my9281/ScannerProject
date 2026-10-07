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
            BaseFileText.Text = baseData.MonthlySource == null ? "尚未导入基础表，请先在主界面导入。" : "基础表：" + Path.GetFileName(baseData.BaseDataFile);
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
            SetBusy(true); RentStatusText.Text = "正在导入 Web 上架表…";
            try
            {
                var rows = await _palletApi.GetWarehouseInventoryAsync(System.Configuration.ConfigurationManager.AppSettings["UploadApiKey"]);
                _inventory = rows; _inventoryPath = null;
                SnapshotDateBox.SelectedDate = DateTime.Today;
                InventoryText.Text = "Web 上架表：" + rows.Count + " 条，导入时间 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                RentStatusText.Text = "导入完成，请确认清单日期和结算年月。";
            }
            catch (Exception ex) { RentStatusText.Text = "导入失败：" + ex.Message; }
            finally { SetBusy(false); }
        }
        private async void ImportInventoryFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Title = "导入在仓清单", Filter = "Excel 工作簿 (*.xlsx)|*.xlsx" };
            if (dialog.ShowDialog(this) != true) return;
            SetBusy(true); RentStatusText.Text = "正在导入在仓清单…";
            try
            {
                var rows = await Task.Run(() => WarehouseRentService.Import(dialog.FileName));
                _inventory = rows; _inventoryPath = dialog.FileName;
                InventoryText.Text = Path.GetFileName(dialog.FileName) + "：" + rows.Count + " 条";
                RentStatusText.Text = "导入完成，请填写这份清单的实际日期。";
            }
            catch (Exception ex) { RentStatusText.Text = "导入失败：" + ex.Message; }
            finally { SetBusy(false); }
        }
        private void InventoryTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog { Filter = "Excel 工作簿 (*.xlsx)|*.xlsx", FileName = "在仓清单导入模板.xlsx" };
            if (dialog.ShowDialog(this) != true) return;
            if (IsSourcePath(dialog.FileName)) { MessageBox.Show(this, "请选择新文件名，保留已导入的源文件。"); return; }
            try { WarehouseRentService.ExportTemplate(dialog.FileName); RentStatusText.Text = "已生成与 Web 上架表字段一致的导入模板，至少填写 SN。"; }
            catch (Exception ex) { RentStatusText.Text = "模板导出失败：" + ex.Message; }
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
            { MessageBox.Show(this, "请输入有效的结算年月和清单日期。", "仓租结算"); return; }
            int month = (int)RentMonthBox.SelectedItem;
            DateTime snapshot = SnapshotDateBox.SelectedDate.Value;
            if (snapshot.Date <= new DateTime(year, month, DateTime.DaysInMonth(year, month))) { MessageBox.Show(this, "清单日期必须在结算月份之后，例如用10月清单结算9月。", "仓租结算"); return; }
            var dialog = new SaveFileDialog { Filter = "Excel 工作簿 (*.xlsx)|*.xlsx", FileName = year + "年" + month + "月-仓租结算.xlsx", DefaultExt = ".xlsx", AddExtension = true };
            if (dialog.ShowDialog(this) != true) return;
            if (IsSourcePath(dialog.FileName)) { MessageBox.Show(this, "请选择新文件名，保留已导入的源文件。"); return; }
            SetBusy(true); RentStatusText.Text = "正在匹配并结算…";
            try
            {
                var result = await Task.Run(() =>
                {
                    var calculation = WarehouseRentService.Calculate(_result, _inventory, _baseData.MonthlySource, year, month, snapshot);
                    WarehouseRentService.Export(calculation, dialog.FileName); return calculation;
                });
                RentStatusText.Text = "已统计 " + result.Rows.Count + " 台次，其中已计算 " + result.Rows.Count(r => r.MonthDays.HasValue) + " 台次，当月合计 " + result.Rows.Sum(r => (long)(r.MonthDays ?? 0)) + " 天，累计合计 " + result.Rows.Sum(r => (long)(r.TotalDays ?? 0)) + " 天。待核对 " + result.Issues.Count + " 条，结算月份之后入仓略过 " + result.FutureReceiptCount + " 条。";
                MessageBox.Show(this, RentStatusText.Text + "\n" + dialog.FileName, "仓租结算完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { RentStatusText.Text = "结算失败：" + ex.Message; }
            finally { SetBusy(false); }
        }
        private static string Summary(RegistrationRepairResult result)
        {
            string text = "保留 " + result.Rows.Count + " 条，填充日期 " + result.FilledDateCount + " 条，按规则删除 " + result.RemovedItemCount + " 条。";
            if (result.MissingDateCount > 0) text += " 无上一条有效日期，跳过 " + result.MissingDateCount + " 条。";
            if (result.InvalidSystemDateCount > 0) text += " 无效售后系统日期 " + result.InvalidSystemDateCount + " 条导出为空。";
            return text;
        }
        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Title = "选择收件登记表", Filter = "Excel 工作簿 (*.xlsx)|*.xlsx", CheckFileExists = true };
            if (dialog.ShowDialog(this) != true) return;
            SetBusy(true);
            StatusText.Text = "正在清洗登记表…";
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
                StatusText.Text = "导入失败：" + ex.Message;
                MessageBox.Show(this, StatusText.Text, "登记表修复", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { SetBusy(false); }
        }
        private async void WarehouseExport_Click(object sender, RoutedEventArgs e)
        {
            if (_result == null || _baseData.MonthlySource == null) return;
            int year;
            if (!int.TryParse(YearBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out year) || year < 1 || year > 9999 || !(MonthBox.SelectedItem is int))
            {
                MessageBox.Show(this, "请输入有效年份（1—9999）并选择处理月份。", "仓租检测");
                return;
            }
            int month = (int)MonthBox.SelectedItem;
            var dialog = new SaveFileDialog { Title = "导出仓租检测", Filter = "Excel 工作簿 (*.xlsx)|*.xlsx", DefaultExt = ".xlsx", AddExtension = true, FileName = year + "年" + month + "月-仓租检测.xlsx" };
            if (dialog.ShowDialog(this) != true) return;
            string output = Path.GetFullPath(dialog.FileName);
            if (string.Equals(output, Path.GetFullPath(_sourcePath), StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(_baseData.BaseDataFile) && string.Equals(output, Path.GetFullPath(_baseData.BaseDataFile), StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "请选择新的文件名，保留原始登记表和基础表。", "仓租检测");
                return;
            }
            var source = _baseData.MonthlySource;
            SetBusy(true);
            StatusText.Text = "正在按基础表处理时间匹配并筛选…";
            try
            {
                var result = await Task.Run(() => RegistrationRepairService.ExportWarehouseInspection(_result, source, year, month, dialog.FileName));
                StatusText.Text = "已导出 " + result.Count + " 条处理时间在 " + year + "年" + month + "月的匹配记录。";
                if (result.UnmatchedCount > 0) StatusText.Text += " 未匹配 SN " + result.UnmatchedCount + " 条。";
                if (result.OutsideMonthCount > 0) StatusText.Text += " 不在所选处理月份 " + result.OutsideMonthCount + " 条。";
                if (result.InvalidDateCount > 0) StatusText.Text += " 跳过无效处理时间 " + result.InvalidDateCount + " 条匹配明细。";
                if (result.Count == 0) StatusText.Text += " 已生成表头。";
                MessageBox.Show(this, StatusText.Text + "\n" + dialog.FileName, "仓租检测导出完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { StatusText.Text = "导出失败：" + ex.Message; MessageBox.Show(this, StatusText.Text, "仓租检测", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { SetBusy(false); }
        }
        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            if (_result == null) return;
            var dialog = new SaveFileDialog { Title = "导出修复后的登记数据", Filter = "Excel 工作簿 (*.xlsx)|*.xlsx", DefaultExt = ".xlsx", AddExtension = true, FileName = Path.GetFileNameWithoutExtension(_sourcePath) + "-修复.xlsx" };
            if (dialog.ShowDialog(this) != true) return;
            if (string.Equals(Path.GetFullPath(dialog.FileName), Path.GetFullPath(_sourcePath), StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "请选择新的文件名，保留原始登记表。", "登记表修复");
                return;
            }
            SetBusy(true);
            StatusText.Text = "正在导出…";
            try
            {
                await Task.Run(() => RegistrationRepairService.Export(_result, dialog.FileName));
                StatusText.Text = "已导出。" + Summary(_result);
                MessageBox.Show(this, StatusText.Text + "\n" + dialog.FileName, "导出完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { StatusText.Text = "导出失败：" + ex.Message; MessageBox.Show(this, StatusText.Text, "登记表修复", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { SetBusy(false); }
        }
    }
}
