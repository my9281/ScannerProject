using Scanner.Models;
using Scanner.Services;
using System.Text;

namespace MaUIScanner;

public partial class OperationsPage : ContentPage
{
    private const int BeijingToNewJerseyHourOffset = -12;
    private string? _inboundSn, _outboundText, _feeTemplate;
    private IReadOnlyList<InboundChecklistRecord> _inboundRecords => ChecklistDataCache.Records;
    private IList<string> _serialNumbers = new List<string>();
    private IList<OutboundInspectionRecord> _outboundRecords = new List<OutboundInspectionRecord>();

    public OperationsPage() { InitializeComponent(); RefreshBase(); }
    private void RefreshBase()
    {
        GlobalBaseLabel.Text = ChecklistDataCache.BaseDataFile is null ? "尚未导入基础表（本次运行全局共用）" : $"{ChecklistDataCache.BaseDataFile}\n{ChecklistDataCache.Records.Count} 条";
        RefreshInboundStatus();
        _outboundRecords = new List<OutboundInspectionRecord>();
        OutboundStatusLabel.Text = "请选择 SKU / SN 文本，使用全局基础表匹配";
        FeeStatusLabel.Text = "";
        LoadOutbound();
    }

    private async void PickInboundBase_Clicked(object? sender, EventArgs e)
    {
        await RunAsync(async () => { var path = await PickAndCacheAsync("导入全局基础 Excel"); if (path is null) return; ChecklistDataCache.ImportBase(path); RefreshBase(); });
    }
    private async void PickInboundSn_Clicked(object? sender, EventArgs e)
    {
        await RunAsync(async () => { _inboundSn = await PickAndCacheAsync("选择待匹配 SN 文本"); if (_inboundSn is null) return; _serialNumbers = File.ReadAllLines(_inboundSn).Select(x => x.Trim().TrimStart('\uFEFF')).Where(x => x.Length > 0 && !x.Equals("SN", StringComparison.OrdinalIgnoreCase)).ToList(); InboundSnLabel.Text = _inboundSn; RefreshInboundStatus(); });
    }
    private async void ExportInboundMatch_Clicked(object? sender, EventArgs e)
    {
        await RunAsync(async () =>
        {
            if (_inboundRecords.Count == 0 || _serialNumbers.Count == 0) throw new InvalidOperationException("请先选择基础 Excel 和 SN 文本。");
            var bySn = _inboundRecords.Where(x => !string.IsNullOrWhiteSpace(x.Sn)).GroupBy(x => x.Sn.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
            int matched = 0; var lines = new List<string> { "SN\t类型\t检测状态\t处理日期" };
            foreach (string sn in _serialNumbers) { if (bySn.TryGetValue(sn, out var row)) { matched++; lines.Add(string.Join("\t", Clean(sn), Clean(row.Type), Clean(row.DetectionStatus), Clean(row.ProcessingTime))); } else lines.Add($"{Clean(sn)}\t\t不存在\t"); }
            string path = OutputPath("InboundDetectionResult", ".txt"); await File.WriteAllLinesAsync(path, lines, new UTF8Encoding(true)); InboundStatusLabel.Text = $"匹配 {matched}，未匹配 {_serialNumbers.Count - matched}"; await ShareOutputAsync(path);
        });
    }
    private async void ExportCurrentMonth_Clicked(object? sender, EventArgs e)
    {
        await RunAsync(async () =>
        {
            DateTime month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
            var rows = _inboundRecords.Where(x => string.Equals(x.DetectionStatus?.Trim(), "待检测", StringComparison.OrdinalIgnoreCase) && TryDate(x.ProcessingTime, out var d) && d.Year == month.Year && d.Month == month.Month).ToList();
            if (rows.Count == 0) throw new InvalidOperationException("本月没有待检测数据。");
            var lines = new List<string> { "分类,SN,SKU,RMA编号,处理日期" };
            lines.AddRange(rows.Select(x => { TryDate(x.ProcessingTime, out var d); return string.Join(",", Csv((x.Type ?? "").Contains("维修") ? "维修" : "非维修"), Csv(x.Sn), Csv(x.Sku), Csv(x.RmaNumber), Csv(d.ToString("yyyy-MM-dd"))); }));
            string path = OutputPath("CurrentMonthPending_" + month.ToString("yyyyMM"), ".csv"); await File.WriteAllLinesAsync(path, lines, new UTF8Encoding(true)); InboundStatusLabel.Text = $"已生成 {rows.Count} 条待检测记录"; await ShareOutputAsync(path);
        });
    }
    private async void PickOutboundText_Clicked(object? sender, EventArgs e) { await RunAsync(async () => { _outboundText = await PickAndCacheAsync("选择 SKU / SN 文本"); OutboundTextLabel.Text = _outboundText ?? "尚未选择"; LoadOutbound(); }); }
    private void LoadOutbound()
    {
        _outboundRecords = new List<OutboundInspectionRecord>();
        OutboundStatusLabel.Text = "请导入全局基础表并选择 SKU / SN 文本。";
        if (_outboundText is null || ChecklistDataCache.Records.Count == 0) return; _outboundRecords = OutboundInspectionService.Build(_outboundText, ChecklistDataCache.Records); int matched = _outboundRecords.Count(x => x.IsMatched); int skus = _outboundRecords.Where(x => !string.IsNullOrWhiteSpace(x.Sku)).Select(x => x.Sku).Distinct(StringComparer.OrdinalIgnoreCase).Count(); OutboundStatusLabel.Text = $"共 {_outboundRecords.Count} 条，{skus} 个 SKU，匹配 {matched}，未匹配 {_outboundRecords.Count - matched}";
    }
    private async void ExportOutbound_Clicked(object? sender, EventArgs e) { await RunAsync(async () => { if (_outboundRecords.Count == 0) throw new InvalidOperationException("请先选择两个来源文件。"); string path = OutputPath("出库检测结果", ".xlsx"); OutboundInspectionXlsxWriter.Write(path, _outboundRecords); await ShareOutputAsync(path); }); }
    private async void PickFeeTemplate_Clicked(object? sender, EventArgs e) { await RunAsync(async () => { _feeTemplate = await PickAndCacheAsync("选择库位费模板 Excel"); FeeTemplateLabel.Text = _feeTemplate ?? "尚未选择"; }); }
    private async void ExportFee_Clicked(object? sender, EventArgs e)
    {
        await RunAsync(async () => { if (_feeTemplate is null || ChecklistDataCache.Records.Count == 0) throw new InvalidOperationException("请先选择模板和基础 Excel。"); string path = OutputPath("库位付费比对结果", ".xlsx"); var summary = LocationFeeComparisonService.Build(_feeTemplate, ChecklistDataCache.Records, path); FeeStatusLabel.Text = $"{summary.SheetCount} 个工作表，{summary.RowCount} 行，匹配 {summary.MatchedCount}，未匹配 {summary.UnmatchedCount}"; await ShareOutputAsync(path); });
    }

    private void RefreshInboundStatus() => InboundStatusLabel.Text = $"基础数据 {_inboundRecords.Count} 条；SN {_serialNumbers.Count} 条";
    private static async Task<string?> PickAndCacheAsync(string title) { var result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = title }); if (result is null) return null; string path = Path.Combine(FileSystem.CacheDirectory, $"{Guid.NewGuid():N}_{result.FileName}"); await using var input = await result.OpenReadAsync(); await using var output = File.Create(path); await input.CopyToAsync(output); return path; }
    private static string OutputPath(string name, string extension) => Path.Combine(FileSystem.CacheDirectory, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}");
    private static Task ShareOutputAsync(string path) => Share.Default.RequestAsync(new ShareFileRequest { Title = "保存处理结果", File = new ShareFile(path) });
    private async Task RunAsync(Func<Task> action) { try { IsBusy = true; await action(); } catch (Exception ex) { await DisplayAlertAsync("处理失败", ex.Message, "确定"); } finally { IsBusy = false; } }
    private static bool TryDate(string? value, out DateTime date) { string text = (value ?? "").Trim(); if (DateTime.TryParse(text, out date)) { date = date.AddHours(BeijingToNewJerseyHourOffset); return true; } if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double serial) && serial > 0) { try { date = DateTime.FromOADate(serial).AddHours(BeijingToNewJerseyHourOffset); return true; } catch { } } date = default; return false; }
    private static string Clean(string? value) => (value ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
    private static string Csv(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
}
