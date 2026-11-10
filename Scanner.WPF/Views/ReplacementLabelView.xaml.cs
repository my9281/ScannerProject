using Scanner.WPF.Helpers;
using Scanner.WPF.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Scanner.WPF.Views
{
    public partial class ReplacementLabelView : UserControl
    {
        private readonly ScanService _scan = ScanService.CreateReplacementService();
        private readonly PrintingHelper _printing = new PrintingHelper();
        private readonly MeterModelService _models = new MeterModelService();
        private readonly DialogHelper _dialogs = new DialogHelper();
        private readonly ScanUploadService _upload = new ScanUploadService();

        public ReplacementLabelView()
        {
            InitializeComponent();
            RefreshInfo();
            Loaded += (s, e) => ScanInput.Focus();
        }

        private void ScanInput_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { e.Handled = true; Print(); } }
        private void Print_Click(object sender, RoutedEventArgs e) => Print();

        private void Print()
        {
            string code = (ScanInput.Text ?? string.Empty).Trim();
            string description = string.Join(" / ", FindVisualChildren<CheckBox>(this).Where(x => x.IsChecked == true && x != RepairOption).Select(x => x.Tag as string).Where(x => !string.IsNullOrWhiteSpace(x)));
            if (string.IsNullOrWhiteSpace(code)) { Error(Scanner.WPF.Helpers.UiText.Get("WpfPrompt001")); return; }
            if (string.IsNullOrWhiteSpace(description)) { Error(Scanner.WPF.Helpers.UiText.Get("WpfPrompt002")); return; }
            try
            {
                if (ScanService.IsGs1AreaCode(code)) { Error(Scanner.WPF.Helpers.UiText.Get("WpfPrompt003")); return; }
                ScanResult result = _scan.Record(code);
                if (result.Oid.IsOid && !result.Oid.ShouldPrint)
                {
                    _scan.RecordWorkbook(result, string.Empty);
                    StatusText.Foreground = Brushes.DarkBlue;
                    StatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfPrompt004");
                    ScanInput.Clear(); RefreshInfo(); ScanInput.Focus();
                    return;
                }
                string model = result.Oid.IsOid ? string.Empty : _models.FindModel(result.Code);
                if (string.IsNullOrWhiteSpace(model)) model = _dialogs.SelectMeterModel(_models);
                if (string.IsNullOrWhiteSpace(model)) { _scan.RecordWorkbook(result, string.Empty); Error(Scanner.WPF.Helpers.UiText.Get("WpfPrompt005")); return; }
                _scan.RecordWorkbook(result, model);
                _printing.PrintReplacementLabel(result.Code, 1, null, model, WidePaper.IsChecked == true ? PrintingHelper.DefaultPaperSize : PrintingHelper.SquarePaperSize, description, RepairOption.IsChecked == true);
                StatusText.Foreground = Brushes.DarkBlue;
                StatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfPrompt006") + description;
                ScanInput.Clear(); RefreshInfo(); ScanInput.Focus();
            }
            catch (Exception ex) { _scan.WriteError(ex); Error(Scanner.WPF.Helpers.UiText.Get("WpfPrompt007") + ex.Message); }
        }

        private void OpenLog_Click(object sender, RoutedEventArgs e) { try { _scan.OpenLog(); } catch (Exception ex) { Error(ex.Message); } }
        private void OpenExcel_Click(object sender, RoutedEventArgs e) { try { _scan.OpenWorkbook(); } catch (Exception ex) { Error(ex.Message); } }
        private void NewFiles_Click(object sender, RoutedEventArgs e) { try { _scan.CreateNewRecordFiles(); RefreshInfo(); StatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfPrompt008"); } catch (Exception ex) { Error(ex.Message); } }
        private async void Upload_Click(object sender, RoutedEventArgs e) { try { await _upload.UploadAsync(_scan.LogFilePath); StatusText.Text = Scanner.WPF.Helpers.UiText.Get("WpfPrompt009"); } catch (Exception ex) { Error(ex.Message); } }
        private void RefreshInfo() { FileText.Text = Scanner.WPF.Helpers.UiText.Get("LogFile") + _scan.LogFilePath + Environment.NewLine + Scanner.WPF.Helpers.UiText.Get("WpfPrompt010") + System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_scan.LogFilePath), "replacement-label.config"); CountText.Text = Scanner.WPF.Helpers.UiText.Get("WpfPrompt011") + _scan.ScanCount; }
        private void Error(string message) { StatusText.Foreground = Brushes.DarkRed; StatusText.Text = message; ScanInput.Focus(); }
        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { DependencyObject child = VisualTreeHelper.GetChild(root, i); if (child is T match) yield return match; foreach (T nested in FindVisualChildren<T>(child)) yield return nested; } }
    }
}
