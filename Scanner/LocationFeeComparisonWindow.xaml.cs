using Microsoft.Win32;
using Scanner.Services;
using System;
using System.Diagnostics;
using System.Windows;

namespace Scanner
{
    public partial class LocationFeeComparisonWindow : Window
    {
        private string _templatePath;
        private string _baseWorkbookPath;

        public LocationFeeComparisonWindow()
        {
            InitializeComponent();
        }

        private void SelectTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Title = "选择库位付费模板", Filter = "Excel 工作簿 (*.xlsx)|*.xlsx", CheckFileExists = true };
            if (dialog.ShowDialog(this) != true) return;
            _templatePath = dialog.FileName;
            TemplateFileTextBlock.Text = _templatePath;
            RefreshStatus();
        }

        private void SelectBaseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Title = "选择 InboundDetection 基础表", Filter = "Excel 工作簿 (*.xlsx)|*.xlsx", CheckFileExists = true };
            if (dialog.ShowDialog(this) != true) return;
            _baseWorkbookPath = dialog.FileName;
            BaseFileTextBlock.Text = _baseWorkbookPath;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            ExportButton.IsEnabled = !string.IsNullOrWhiteSpace(_templatePath) && !string.IsNullOrWhiteSpace(_baseWorkbookPath);
            StatusTextBlock.Text = ExportButton.IsEnabled ? "文件已就绪，可以生成比对结果。" : "请选择模板和基础表。";
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "保存库位付费比对结果",
                Filter = "Excel 工作簿 (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                AddExtension = true,
                FileName = "库位付费比对结果_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx"
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                ExportButton.IsEnabled = false;
                StatusTextBlock.Text = "正在处理两个工作表……";
                LocationFeeComparisonSummary summary = LocationFeeComparisonService.Build(_templatePath, _baseWorkbookPath, dialog.FileName);
                StatusTextBlock.Text = string.Format("处理完成：{0} 个 sheet，共 {1:N0} 条 SN，匹配 {2:N0} 条，未匹配 {3:N0} 条。", summary.SheetCount, summary.RowCount, summary.MatchedCount, summary.UnmatchedCount);
                MessageBox.Show(this, StatusTextBlock.Text + "\n\n已保存：" + dialog.FileName, "库位付费比对", MessageBoxButton.OK, MessageBoxImage.Information);
                Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "处理失败。";
                MessageBox.Show(this, "生成库位付费比对结果失败：" + ex.Message, "库位付费比对", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ExportButton.IsEnabled = !string.IsNullOrWhiteSpace(_templatePath) && !string.IsNullOrWhiteSpace(_baseWorkbookPath);
            }
        }
    }
}
