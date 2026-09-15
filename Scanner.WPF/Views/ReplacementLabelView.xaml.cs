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
            string description = string.Join(" / ", FindVisualChildren<CheckBox>(this).Where(x => x.IsChecked == true).Select(x => x.Tag as string).Where(x => !string.IsNullOrWhiteSpace(x)));
            if (string.IsNullOrWhiteSpace(code)) { Error("请扫描或输入编号。"); return; }
            if (string.IsNullOrWhiteSpace(description)) { Error("请至少选择一个标签内容。"); return; }
            try
            {
                if (ScanService.IsGs1AreaCode(code)) { Error("检测到单独的 GS1 地区码，未处理。"); return; }
                ScanResult result = _scan.Record(code);
                string model = result.Oid.IsOid ? string.Empty : _models.FindModel(result.Code);
                if (string.IsNullOrWhiteSpace(model)) model = _dialogs.SelectMeterModel(_models);
                if (string.IsNullOrWhiteSpace(model)) { Error("已取消型号选择。"); return; }
                _scan.RecordWorkbook(result, model);
                _printing.PrintReplacementLabel(result.Code, 1, null, model, WidePaper.IsChecked == true ? PrintingHelper.DefaultPaperSize : PrintingHelper.SquarePaperSize, description);
                StatusText.Foreground = Brushes.DarkBlue;
                StatusText.Text = "已打印替换标签：" + description;
                ScanInput.Clear(); RefreshInfo(); ScanInput.Focus();
            }
            catch (Exception ex) { _scan.WriteError(ex); Error("替换标签打印失败：" + ex.Message); }
        }

        private void OpenLog_Click(object sender, RoutedEventArgs e) { try { _scan.OpenLog(); } catch (Exception ex) { Error(ex.Message); } }
        private void OpenExcel_Click(object sender, RoutedEventArgs e) { try { _scan.OpenWorkbook(); } catch (Exception ex) { Error(ex.Message); } }
        private void NewFiles_Click(object sender, RoutedEventArgs e) { try { _scan.CreateNewRecordFiles(); RefreshInfo(); StatusText.Text = "已新建替换标签记录文件。"; } catch (Exception ex) { Error(ex.Message); } }
        private async void Upload_Click(object sender, RoutedEventArgs e) { try { await _upload.UploadAsync(_scan.LogFilePath); StatusText.Text = "替换标签记录上传成功。"; } catch (Exception ex) { Error(ex.Message); } }
        private void RefreshInfo() { FileText.Text = "记录文件：" + _scan.LogFilePath + Environment.NewLine + "配置文件：" + System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_scan.LogFilePath), "replacement-label.config"); CountText.Text = "本次已记录：" + _scan.ScanCount; }
        private void Error(string message) { StatusText.Foreground = Brushes.DarkRed; StatusText.Text = message; ScanInput.Focus(); }
        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { DependencyObject child = VisualTreeHelper.GetChild(root, i); if (child is T match) yield return match; foreach (T nested in FindVisualChildren<T>(child)) yield return nested; } }
    }
}
