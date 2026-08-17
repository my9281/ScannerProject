using Microsoft.Win32;
using Scanner.Models;
using Scanner.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace Scanner
{
    public partial class ChecklistWindow : Window
    {
        public ChecklistWindow()
        {
            InitializeComponent();
            RefreshStatus();
        }

        private void ImportBaseDataButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Title = "选择基础数据文件", Filter = "Excel 工作簿 (*.xlsx)|*.xlsx", CheckFileExists = true };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                IList<InboundChecklistRecord> records = ChecklistXlsxReader.Read(dialog.FileName);
                ChecklistDataCache.ReplaceRecords(records, dialog.FileName);
                RefreshStatus();
            }
            catch (Exception ex) { ShowError("导入基础数据失败：" + ex.Message); }
        }

        private void ImportSnButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Title = "选择 SN 清单", Filter = "文本文件 (*.txt)|*.txt", CheckFileExists = true };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                List<string> serialNumbers = File.ReadAllLines(dialog.FileName)
                    .Select(line => line.Trim().TrimStart('\uFEFF'))
                    .Where(line => !string.IsNullOrWhiteSpace(line) && !string.Equals(line, "SN", StringComparison.OrdinalIgnoreCase)).ToList();
                ChecklistDataCache.ReplaceSerialNumbers(serialNumbers, dialog.FileName);
                RefreshStatus();
            }
            catch (Exception ex) { ShowError("导入 SN 清单失败：" + ex.Message); }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "保存匹配结果", Filter = "文本文件 (*.txt)|*.txt", AddExtension = true, DefaultExt = ".txt",
                FileName = "InboundDetectionResult_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt"
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                Dictionary<string, InboundChecklistRecord> recordBySn = ChecklistDataCache.Records
                    .Where(record => !string.IsNullOrWhiteSpace(record.Sn))
                    .GroupBy(record => record.Sn.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
                int matchedCount = 0;
                List<string> lines = new List<string> { "SN\t类型\t检测状态\t处理日期" };
                foreach (string sn in ChecklistDataCache.SerialNumbers)
                {
                    InboundChecklistRecord record;
                    if (recordBySn.TryGetValue(sn, out record))
                    {
                        matchedCount++;
                        lines.Add(string.Join("\t", Clean(sn), Clean(record.Type), Clean(record.DetectionStatus), Clean(record.ProcessingTime)));
                    }
                    else lines.Add(Clean(sn) + "\t不存在");
                }
                File.WriteAllLines(dialog.FileName, lines, new UTF8Encoding(true));
                MessageBox.Show(this, string.Format("导出完成。\n匹配：{0:N0} 条\n不存在：{1:N0} 条", matchedCount, ChecklistDataCache.SerialNumbers.Count - matchedCount), "入库检测", MessageBoxButton.OK, MessageBoxImage.Information);
                Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
            }
            catch (Exception ex) { ShowError("导出失败：" + ex.Message); }
        }

        private void RefreshStatus()
        {
            BaseStatusTextBlock.Text = ChecklistDataCache.Records.Count == 0 ? "基础数据：尚未导入" : string.Format("基础数据：已缓存 {0:N0} 条（{1:yyyy-MM-dd HH:mm:ss}）", ChecklistDataCache.Records.Count, ChecklistDataCache.BaseDataImportedAt);
            BaseFileTextBlock.Text = ChecklistDataCache.BaseDataFile ?? string.Empty;
            SnStatusTextBlock.Text = ChecklistDataCache.SerialNumbers.Count == 0 ? "SN 清单：尚未导入" : string.Format("SN 清单：已缓存 {0:N0} 个（{1:yyyy-MM-dd HH:mm:ss}）", ChecklistDataCache.SerialNumbers.Count, ChecklistDataCache.SnImportedAt);
            SnFileTextBlock.Text = ChecklistDataCache.SnFile ?? string.Empty;
            ExportButton.IsEnabled = ChecklistDataCache.Records.Count > 0 && ChecklistDataCache.SerialNumbers.Count > 0;
        }

        private void ShowError(string message) { MessageBox.Show(this, message, "入库检测", MessageBoxButton.OK, MessageBoxImage.Error); }
        private static string Clean(string value) { return (value ?? string.Empty).Replace("\t", " ").Replace("\r", " ").Replace("\n", " "); }
    }
}
