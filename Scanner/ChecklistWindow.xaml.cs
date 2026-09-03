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
        private const int BeijingToNewJerseyHourOffset = -12;

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
                    else lines.Add(string.Join("\t", Clean(sn), string.Empty, "不存在", string.Empty));
                }
                File.WriteAllLines(dialog.FileName, lines, new UTF8Encoding(true));
                MessageBox.Show(this, string.Format("导出完成。\n匹配：{0:N0} 条\n不存在：{1:N0} 条", matchedCount, ChecklistDataCache.SerialNumbers.Count - matchedCount), "入库检测", MessageBoxButton.OK, MessageBoxImage.Information);
                Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
            }
            catch (Exception ex) { ShowError("导出失败：" + ex.Message); }
        }

        private void ExportCurrentMonthButton_Click(object sender, RoutedEventArgs e)
        {
            DateTime currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            List<InboundChecklistRecord> records = ChecklistDataCache.Records.Where(record =>
            {
                DateTime processingDate;
                return string.Equals((record.DetectionStatus ?? string.Empty).Trim(), "待检测", StringComparison.OrdinalIgnoreCase)
                    && TryGetProcessingDate(record.ProcessingTime, out processingDate)
                    && IsSameMonth(processingDate, currentMonth);
            }).ToList();

            if (records.Count == 0)
            {
                MessageBox.Show(this, "当前月没有待检测数据。", "导出 CSV", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "导出当前月待检测数据",
                Filter = "CSV 文件 (*.csv)|*.csv",
                AddExtension = true,
                DefaultExt = ".csv",
                FileName = "CurrentMonthPending_" + currentMonth.ToString("yyyyMM") + ".csv"
            };
            if (dialog.ShowDialog(this) != true) return;

            try
            {
                var lines = new List<string> { "分类,SN,SKU,RMA编号,处理日期" };
                lines.AddRange(records.Select(record =>
                {
                    DateTime processingDate;
                    TryGetProcessingDate(record.ProcessingTime, out processingDate);
                    string category = (record.Type ?? string.Empty).IndexOf("维修", StringComparison.OrdinalIgnoreCase) >= 0 ? "维修" : "非维修";
                    return string.Join(",", Csv(category), Csv(record.Sn), Csv(record.Sku), Csv(record.RmaNumber), Csv(processingDate.ToString("yyyy-MM-dd")));
                }));
                File.WriteAllLines(dialog.FileName, lines, new UTF8Encoding(true));
                int repairCount = records.Count(record => (record.Type ?? string.Empty).IndexOf("维修", StringComparison.OrdinalIgnoreCase) >= 0);
                MessageBox.Show(this, string.Format("CSV 导出完成。\n维修：{0:N0} 条\n非维修：{1:N0} 条", repairCount, records.Count - repairCount), "导出 CSV", MessageBoxButton.OK, MessageBoxImage.Information);
                Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
            }
            catch (Exception ex) { ShowError("导出当前月待检测 CSV 失败：" + ex.Message); }
        }

        private void RefreshStatus()
        {
            BaseStatusTextBlock.Text = ChecklistDataCache.Records.Count == 0 ? "基础数据：尚未导入" : string.Format("基础数据：已缓存 {0:N0} 条（{1:yyyy-MM-dd HH:mm:ss}）", ChecklistDataCache.Records.Count, ChecklistDataCache.BaseDataImportedAt);
            BaseFileTextBlock.Text = ChecklistDataCache.BaseDataFile ?? string.Empty;
            SnStatusTextBlock.Text = ChecklistDataCache.SerialNumbers.Count == 0 ? "SN 清单：尚未导入" : string.Format("SN 清单：已缓存 {0:N0} 个（{1:yyyy-MM-dd HH:mm:ss}）", ChecklistDataCache.SerialNumbers.Count, ChecklistDataCache.SnImportedAt);
            SnFileTextBlock.Text = ChecklistDataCache.SnFile ?? string.Empty;
            ExportButton.IsEnabled = ChecklistDataCache.Records.Count > 0 && ChecklistDataCache.SerialNumbers.Count > 0;
            ExportCurrentMonthButton.IsEnabled = ChecklistDataCache.Records.Count > 0;
            DateTime currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            CurrentMonthColumn.Header = "当前月 " + currentMonth.ToString("yyyy-MM");
            PreviousMonthColumn.Header = "上一个月 " + currentMonth.AddMonths(-1).ToString("yyyy-MM");
            TwoMonthsAgoColumn.Header = "上两个月 " + currentMonth.AddMonths(-2).ToString("yyyy-MM");
            StatusSummaryDataGrid.ItemsSource = BuildStatusSummaries(ChecklistDataCache.Records, currentMonth);
        }

        private static IList<ChecklistStatusSummary> BuildStatusSummaries(IEnumerable<InboundChecklistRecord> records, DateTime currentMonth)
        {
            DateTime previousMonth = currentMonth.AddMonths(-1);
            DateTime twoMonthsAgo = currentMonth.AddMonths(-2);
            var summaries = new List<ChecklistStatusSummary>
            {
                new ChecklistStatusSummary("待检测 - 维修"),
                new ChecklistStatusSummary("待检测 - 非维修"),
                new ChecklistStatusSummary("完成")
            };

            foreach (InboundChecklistRecord record in records ?? Enumerable.Empty<InboundChecklistRecord>())
            {
                DateTime processingDate;
                int bucket = TryGetProcessingDate(record.ProcessingTime, out processingDate)
                    ? IsSameMonth(processingDate, currentMonth) ? 0
                    : IsSameMonth(processingDate, previousMonth) ? 1
                    : IsSameMonth(processingDate, twoMonthsAgo) ? 2
                    : 3
                    : 3;
                bool pending = string.Equals((record.DetectionStatus ?? string.Empty).Trim(), "待检测", StringComparison.OrdinalIgnoreCase);
                bool repair = (record.Type ?? string.Empty).IndexOf("维修", StringComparison.OrdinalIgnoreCase) >= 0;
                ChecklistStatusSummary summary = !pending ? summaries[2] : repair ? summaries[0] : summaries[1];
                summary.Increment(bucket);
            }
            int currentTotal = summaries.Sum(item => item.CurrentMonthCount);
            int previousTotal = summaries.Sum(item => item.PreviousMonthCount);
            int twoMonthsAgoTotal = summaries.Sum(item => item.TwoMonthsAgoCount);
            int remainingTotal = summaries.Sum(item => item.RemainingCount);
            foreach (ChecklistStatusSummary summary in summaries)
            {
                summary.CalculateDisplays(currentTotal, previousTotal, twoMonthsAgoTotal, remainingTotal);
            }
            return summaries;
        }

        private static bool IsSameMonth(DateTime value, DateTime month)
        {
            return value.Year == month.Year && value.Month == month.Month;
        }

        private static bool TryGetProcessingDate(string value, out DateTime date)
        {
            string text = (value ?? string.Empty).Trim();
            if (DateTime.TryParse(text, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.AllowWhiteSpaces, out date) ||
                DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AllowWhiteSpaces, out date))
            {
                date = date.AddHours(BeijingToNewJerseyHourOffset);
                return true;
            }

            double serialDate;
            if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out serialDate) && serialDate > 0)
            {
                try
                {
                    date = DateTime.FromOADate(serialDate).AddHours(BeijingToNewJerseyHourOffset);
                    return true;
                }
                catch (ArgumentException) { }
            }
            date = default(DateTime);
            return false;
        }

        private void ShowError(string message) { MessageBox.Show(this, message, "入库检测", MessageBoxButton.OK, MessageBoxImage.Error); }
        private static string Clean(string value) { return (value ?? string.Empty).Replace("\t", " ").Replace("\r", " ").Replace("\n", " "); }
        private static string Csv(string value) { return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\""; }

        private sealed class ChecklistStatusSummary
        {
            public ChecklistStatusSummary(string category) { Category = category; }
            public string Category { get; private set; }
            public int CurrentMonthCount { get; private set; }
            public int PreviousMonthCount { get; private set; }
            public int TwoMonthsAgoCount { get; private set; }
            public int RemainingCount { get; private set; }
            public string CurrentMonthDisplay { get; private set; }
            public string PreviousMonthDisplay { get; private set; }
            public string TwoMonthsAgoDisplay { get; private set; }
            public string RemainingDisplay { get; private set; }
            public string TotalDisplay { get; private set; }

            public void Increment(int bucket)
            {
                if (bucket == 0) CurrentMonthCount++;
                else if (bucket == 1) PreviousMonthCount++;
                else if (bucket == 2) TwoMonthsAgoCount++;
                else RemainingCount++;
            }

            public void CalculateDisplays(int currentTotal, int previousTotal, int twoMonthsAgoTotal, int remainingTotal)
            {
                CurrentMonthDisplay = FormatCount(CurrentMonthCount, currentTotal);
                PreviousMonthDisplay = FormatCount(PreviousMonthCount, previousTotal);
                TwoMonthsAgoDisplay = FormatCount(TwoMonthsAgoCount, twoMonthsAgoTotal);
                RemainingDisplay = FormatCount(RemainingCount, remainingTotal);
                TotalDisplay = (CurrentMonthCount + PreviousMonthCount + TwoMonthsAgoCount + RemainingCount).ToString("N0");
            }

            private static string FormatCount(int count, int total)
            {
                double percentage = total == 0 ? 0 : count * 100d / total;
                return string.Format("{0:N0}（{1:0.0}%）", count, percentage);
            }
        }
    }
}
