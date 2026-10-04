using Microsoft.Win32;
using Scanner.Helpers.Services;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Scanner.WPF
{
    public partial class MonthlyReportWindow : Window
    {
        private readonly MonthlyReportSource _source;
        private bool _exporting;
        private MonthlyMaterialSource _detectionMaterials;
        private MonthlyMaterialSource _maintenanceMaterials;
        public MonthlyReportWindow(MonthlyReportSource source)
        {
            InitializeComponent();
            _source = source;
            YearBox.ItemsSource = source.Years.Concat(new[] { DateTime.Today.Year }).Distinct().OrderByDescending(x => x).ToList();
            YearBox.SelectedItem = source.Years.Count > 0 ? source.Years.Max() : DateTime.Today.Year;
            MonthBox.ItemsSource = Enumerable.Range(1, 12).ToList();
            MonthBox.SelectedItem = DateTime.Today.Month;
            StatusText.Text = source.InvalidDateCount > 0 ? "基础表有 " + source.InvalidDateCount + " 条记录的处理时间为空或无效，导出时将跳过。" : "请选择年月和导出位置。";
            Closing += (sender, e) => { if (_exporting) e.Cancel = true; };
        }
        private void UploadDetection_Click(object sender, RoutedEventArgs e) => UploadMaterials(true);
        private void UploadMaintenance_Click(object sender, RoutedEventArgs e) => UploadMaterials(false);
        private void UploadMaterials(bool detection)
        {
            var dialog = new OpenFileDialog { Title = detection ? "上传 InboundDetection" : "上传 ProductMaintenance", Filter = "Excel 工作簿 (*.xlsx)|*.xlsx" };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                var source = MonthlyMaterialSource.Load(dialog.FileName);
                if (detection) { _detectionMaterials = source; DetectionFileText.Text = System.IO.Path.GetFileName(dialog.FileName); }
                else { _maintenanceMaterials = source; MaintenanceFileText.Text = System.IO.Path.GetFileName(dialog.FileName); }
                StatusText.Text = "附加表已导入，导出时将合并统计所选年月的物料申领数量。";
                if (source.InvalidDateCount > 0) StatusText.Text += " 该表有 " + source.InvalidDateCount + " 条处理时间为空或无效，统计时将跳过。";
            }
            catch (Exception ex) { MessageBox.Show(this, "导入失败：" + ex.Message, "附加表导入", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            int year;
            if (!int.TryParse(YearBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out year) || year < 1 || year > 9999 || !(MonthBox.SelectedItem is int))
            {
                MessageBox.Show(this, "请输入有效年份（1—9999）并选择月份。", "月报导出");
                return;
            }
            int month = (int)MonthBox.SelectedItem;
            var dialog = new SaveFileDialog { Title = "导出月报", Filter = "Excel 工作簿 (*.xlsx)|*.xlsx", DefaultExt = ".xlsx", AddExtension = true, FileName = year + "年" + month + "月数据.xlsx" };
            if (dialog.ShowDialog(this) != true) return;
            _exporting = true;
            DetectionUploadButton.IsEnabled = MaintenanceUploadButton.IsEnabled = ExportButton.IsEnabled = YearBox.IsEnabled = MonthBox.IsEnabled = false;
            StatusText.Text = "正在导出…";
            try
            {
                var result = await Task.Run(() => MonthlyReportService.Export(_source, year, month, dialog.FileName, _detectionMaterials, _maintenanceMaterials));
                StatusText.Text = "已导出 " + result.Count + " 条当月记录，共 " + result.SheetCount + " 个工作表。";
                if (_detectionMaterials != null || _maintenanceMaterials != null) StatusText.Text += " 物料申领统计共 " + result.MaterialSkuCount + " 个 SKU。";
                if (result.MaterialInvalidDateCount > 0) StatusText.Text += " 附加表跳过无效处理时间 " + result.MaterialInvalidDateCount + " 条。";
                if (result.Count == 0) StatusText.Text += " 所选月份无数据，已生成表头。";
                if (result.InvalidDateCount > 0) StatusText.Text += " 跳过无效处理时间 " + result.InvalidDateCount + " 条。";
                if (result.UnclassifiedCount > 0) StatusText.Text += " 未分类记录 " + result.UnclassifiedCount + " 条保留在月度总表。";
                MessageBox.Show(this, StatusText.Text + "\n" + dialog.FileName, "导出完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { StatusText.Text = "导出失败：" + ex.Message; MessageBox.Show(this, StatusText.Text, "月报导出", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { _exporting = false; DetectionUploadButton.IsEnabled = MaintenanceUploadButton.IsEnabled = ExportButton.IsEnabled = YearBox.IsEnabled = MonthBox.IsEnabled = true; }
        }
    }
}
