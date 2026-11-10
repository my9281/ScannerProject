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
            StatusText.Text = source.InvalidDateCount > 0 ? Scanner.WPF.Helpers.UiText.Get("WpfComplete148") + source.InvalidDateCount + Scanner.WPF.Helpers.UiText.Get("WpfComplete149") : Scanner.WPF.Helpers.UiText.Get("WpfComplete150");
            Closing += (sender, e) => { if (_exporting) e.Cancel = true; };
        }
        private void UploadDetection_Click(object sender, RoutedEventArgs e) => UploadMaterials(true);
        private void UploadMaintenance_Click(object sender, RoutedEventArgs e) => UploadMaterials(false);
        private void UploadMaterials(bool detection)
        {
            var dialog = new OpenFileDialog { Title = detection ? Scanner.WPF.Helpers.UiText.Get("WpfComplete067") : Scanner.WPF.Helpers.UiText.Get("WpfComplete069"), Filter = Scanner.WPF.Helpers.UiText.Get("ExcelFileFilter") };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                var source = MonthlyMaterialSource.Load(dialog.FileName);
                if (detection) { _detectionMaterials = source; DetectionFileText.Text = System.IO.Path.GetFileName(dialog.FileName); }
                else { _maintenanceMaterials = source; MaintenanceFileText.Text = System.IO.Path.GetFileName(dialog.FileName); }
                StatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete151");
                if (source.InvalidDateCount > 0) StatusText.Text += Scanner.WPF.Helpers.UiText.Get("WpfComplete152") + source.InvalidDateCount + Scanner.WPF.Helpers.UiText.Get("WpfComplete153");
            }
            catch (Exception ex) { MessageBox.Show(this, Scanner.WPF.Helpers.UiText.Get("ImportFailedPrefix") + ex.Message, Scanner.WPF.Helpers.UiText.Get("WpfComplete154"), MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            int year;
            if (!int.TryParse(YearBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out year) || year < 1 || year > 9999 || !(MonthBox.SelectedItem is int))
            {
                MessageBox.Show(this, Scanner.WPF.Helpers.UiText.Get("WpfComplete155"), Scanner.WPF.Helpers.UiText.Get("WpfComplete046"));
                return;
            }
            int month = (int)MonthBox.SelectedItem;
            var dialog = new SaveFileDialog { Title = Scanner.WPF.Helpers.UiText.Get("WpfComplete156"), Filter = Scanner.WPF.Helpers.UiText.Get("ExcelFileFilter"), DefaultExt = ".xlsx", AddExtension = true, FileName = year + Scanner.WPF.Helpers.UiText.Get("WpfComplete157") + month + Scanner.WPF.Helpers.UiText.Get("WpfComplete158") };
            if (dialog.ShowDialog(this) != true) return;
            _exporting = true;
            DetectionUploadButton.IsEnabled = MaintenanceUploadButton.IsEnabled = ExportButton.IsEnabled = YearBox.IsEnabled = MonthBox.IsEnabled = false;
            StatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete159");
            try
            {
                var result = await Task.Run(() => MonthlyReportService.Export(_source, year, month, dialog.FileName, _detectionMaterials, _maintenanceMaterials));
                StatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfComplete160") + result.Count + Scanner.WPF.Helpers.UiText.Get("WpfComplete161") + result.SheetCount + Scanner.WPF.Helpers.UiText.Get("WpfComplete162");
                if (_detectionMaterials != null || _maintenanceMaterials != null) StatusText.Text += Scanner.WPF.Helpers.UiText.Get("WpfComplete163") + result.MaterialSkuCount + Scanner.WPF.Helpers.UiText.Get("WpfComplete164");
                if (result.MaterialInvalidDateCount > 0) StatusText.Text += Scanner.WPF.Helpers.UiText.Get("WpfComplete165") + result.MaterialInvalidDateCount + Scanner.WPF.Helpers.UiText.Get("WpfComplete166");
                if (result.Count == 0) StatusText.Text += Scanner.WPF.Helpers.UiText.Get("WpfComplete167");
                if (result.InvalidDateCount > 0) StatusText.Text += Scanner.WPF.Helpers.UiText.Get("WpfComplete168") + result.InvalidDateCount + Scanner.WPF.Helpers.UiText.Get("WpfComplete166");
                if (result.UnclassifiedCount > 0) StatusText.Text += Scanner.WPF.Helpers.UiText.Get("WpfComplete169") + result.UnclassifiedCount + Scanner.WPF.Helpers.UiText.Get("WpfComplete170");
                MessageBox.Show(this, StatusText.Text + "\n" + dialog.FileName, Scanner.WPF.Helpers.UiText.Get("ExportComplete"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { StatusText.Text = Scanner.WPF.Helpers.UiText.Get("ExportFailedPrefix") + ex.Message; MessageBox.Show(this, StatusText.Text, Scanner.WPF.Helpers.UiText.Get("WpfComplete046"), MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { _exporting = false; DetectionUploadButton.IsEnabled = MaintenanceUploadButton.IsEnabled = ExportButton.IsEnabled = YearBox.IsEnabled = MonthBox.IsEnabled = true; }
        }
    }
}
