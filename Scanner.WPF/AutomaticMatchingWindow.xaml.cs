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
            StatusText.Text = "正在获取 PDA 托盘数据…";
            try
            {
                var inbound = _baseData.Records.ToArray();
                var pallets = await _api.GetPdaPalletsAsync(System.Configuration.ConfigurationManager.AppSettings["UploadApiKey"]);
                _rows = AutomaticMatchingService.Match(pallets, inbound);
                ResultsGrid.ItemsSource = _rows;
                StatusText.Text = $"已获取 {pallets.Count} 条托盘记录，匹配待检测明细 {_rows.Count} 条，涉及 {_rows.Select(x => x.PalletNumber).Distinct().Count()} 个托盘。";
                ExportButton.IsEnabled = _rows.Count > 0;
                if (_rows.Count > 0) await UploadRowsAsync();
            }
            catch (Exception ex) { StatusText.Text = "匹配失败：" + ex.Message; }
            finally { FetchButton.IsEnabled = true; }
        }
        private async System.Threading.Tasks.Task UploadRowsAsync()
        {
            StatusText.Text = $"已匹配 {_rows.Count} 条，正在上传工单给 my9281…";
            UploadButton.IsEnabled = false;
            try
            {
                var result = await _api.UploadMatchedWorkOrdersAsync(_rows, System.Configuration.ConfigurationManager.AppSettings["UploadApiKey"]);
                StatusText.Text = $"匹配 {_rows.Count} 条，新增工单 {result.InsertedCount} 条，跳过重复 {result.SkippedCount} 条。分配用户：{result.Username}，审核位：0，未完成。";
            }
            catch (Exception ex)
            {
                StatusText.Text = "匹配结果已保留，工单上传未全部完成：" + ex.Message;
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
            var dialog = new SaveFileDialog { Filter = "Excel 工作簿 (*.xlsx)|*.xlsx", FileName = "自动匹配_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx" };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                if (string.Equals(Path.GetFullPath(dialog.FileName), Path.GetFullPath(_baseData.BaseDataFile), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("请另存为新文件，不能覆盖基础表。");
                AutomaticMatchingService.Export(dialog.FileName, _rows);
                StatusText.Text = "已导出：" + dialog.FileName;
            }
            catch (Exception ex) { MessageBox.Show(this, "导出失败：" + ex.Message); }
        }
    }
}
