using Scanner.Controllers;
using Scanner.Helpers.Services;
using Scanner.MaUI.Services;
using Scanner.MaUI.Views;
using Scanner.Models;
using System.Text;
namespace Scanner.MaUI.Controllers;

public sealed class OperationsPageController : ControllerBase
{
    private static LocalizationService L => LocalizationService.Current;
    private readonly IOperationsPageView _view;
    private readonly Scanner.MaUI.Windows.IWindow _window;
    private readonly ShelvedPalletApiService _palletUpload;

    private const int BeijingToNewJerseyHourOffset = -12;
    private string? _inboundSn, _outboundText, _feeTemplate;
    private IReadOnlyList<InboundChecklistRecord> _inboundRecords => _baseData.Records;
    private IList<string> _serialNumbers = new List<string>();
    private IList<OutboundInspectionRecord> _outboundRecords = new List<OutboundInspectionRecord>();

    private readonly ChecklistDataCache _baseData;
    public OperationsPageController(IOperationsPageView view, Scanner.MaUI.Windows.IWindow window, ChecklistDataCache baseData, ShelvedPalletApiService palletUpload) { _view = view; _window = window; _baseData = baseData; _palletUpload = palletUpload; RefreshBase(); }
    private void RefreshBase()
    {
        _view.GlobalBaseLabel.Text = _baseData.BaseDataFile is null ? L.Get("OperationsBaseMissing") : L.Format("OperationsBaseLoaded", _baseData.BaseDataFile, _baseData.Records.Count);
        RefreshInboundStatus();
        _outboundRecords = new List<OutboundInspectionRecord>();
        _view.OutboundStatusLabel.Text = L.Get("OperationsSelectOutboundHint");
        _view.UploadPalletButton.IsEnabled = false;
        _view.FeeStatusLabel.Text = "";
        LoadOutbound();
    }

    public async void PickInboundBase_Clicked(object? sender, EventArgs e)
    {
        await RunAsync(async () => { var path = await PickAndCacheAsync(L.Get("OperationsPickBase")); if (path is null) return; _baseData.ImportBase(path); RefreshBase(); });
    }
    public async void PickInboundSn_Clicked(object? sender, EventArgs e)
    {
        await RunAsync(async () => { _inboundSn = await PickAndCacheAsync(L.Get("OperationsSelectInboundSn")); if (_inboundSn is null) return; _serialNumbers = File.ReadAllLines(_inboundSn).Select(x => x.Trim().TrimStart('\uFEFF')).Where(x => x.Length > 0 && !x.Equals("SN", StringComparison.OrdinalIgnoreCase)).ToList(); _view.InboundSnLabel.Text = _inboundSn; RefreshInboundStatus(); });
    }
    public async void ExportInboundMatch_Clicked(object? sender, EventArgs e)
    {
        await RunAsync(async () =>
        {
            if (_inboundRecords.Count == 0 || _serialNumbers.Count == 0) throw new InvalidOperationException(L.Get("OperationsNeedInboundSources"));
            var bySn = _inboundRecords.Where(x => !string.IsNullOrWhiteSpace(x.Sn)).GroupBy(x => x.Sn.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
            int matched = 0; var lines = new List<string> { "SN\t类型\t检测状态\t处理日期" };
            foreach (string sn in _serialNumbers) { if (bySn.TryGetValue(sn, out var row)) { matched++; lines.Add(string.Join("\t", Clean(sn), Clean(row.Type), Clean(row.DetectionStatus), Clean(row.ProcessingTime))); } else lines.Add($"{Clean(sn)}\t\t不存在\t"); }
            string path = OutputPath("InboundDetectionResult", ".txt"); await File.WriteAllLinesAsync(path, lines, new UTF8Encoding(true)); _view.InboundStatusLabel.Text = L.Format("OperationsMatchSummary", matched, _serialNumbers.Count - matched); await ShareOutputAsync(path);
        });
    }
    public async void ExportCurrentMonth_Clicked(object? sender, EventArgs e)
    {
        await RunAsync(async () =>
        {
            DateTime month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
            var rows = _inboundRecords.Where(x => string.Equals(x.DetectionStatus?.Trim(), "待检测", StringComparison.OrdinalIgnoreCase) && TryDate(x.ProcessingTime, out var d) && d.Year == month.Year && d.Month == month.Month).ToList();
            if (rows.Count == 0) throw new InvalidOperationException(L.Get("NoPendingThisMonth"));
            var lines = new List<string> { "分类,SN,SKU,RMA编号,处理日期" };
            lines.AddRange(rows.Select(x => { TryDate(x.ProcessingTime, out var d); return string.Join(",", Csv((x.Type ?? "").Contains("维修") ? "维修" : "非维修"), Csv(x.Sn), Csv(x.Sku), Csv(x.RmaNumber), Csv(d.ToString("yyyy-MM-dd"))); }));
            string path = OutputPath("CurrentMonthPending_" + month.ToString("yyyyMM"), ".csv"); await File.WriteAllLinesAsync(path, lines, new UTF8Encoding(true)); _view.InboundStatusLabel.Text = L.Format("OperationsPendingSummary", rows.Count); await ShareOutputAsync(path);
        });
    }
    public async void PickOutboundText_Clicked(object? sender, EventArgs e) { await RunAsync(async () => { _outboundText = await PickAndCacheAsync(L.Get("SelectSkuSnTitle")); _view.OutboundTextLabel.Text = _outboundText ?? L.Get("OperationsNotSelected"); LoadOutbound(); }); }
    private void LoadOutbound()
    {
        _outboundRecords = new List<OutboundInspectionRecord>();
        _view.OutboundStatusLabel.Text = L.Get("OperationsNeedOutboundSources");
        if (_outboundText is null || _baseData.Records.Count == 0) return; _outboundRecords = OutboundInspectionService.Build(_outboundText, _baseData.Records); int matched = _outboundRecords.Count(x => x.IsMatched); int skus = _outboundRecords.Where(x => !string.IsNullOrWhiteSpace(x.Sku)).Select(x => x.Sku).Distinct(StringComparer.OrdinalIgnoreCase).Count(); _view.OutboundStatusLabel.Text = L.Format("OutboundSummary", _outboundRecords.Count, skus, matched, _outboundRecords.Count - matched); _view.UploadPalletButton.IsEnabled = _outboundRecords.Count > 0;
    }
    public async void ExportOutbound_Clicked(object? sender, EventArgs e) { await RunAsync(async () => { if (_outboundRecords.Count == 0) throw new InvalidOperationException(L.Get("OperationsNeedOutboundSources")); string path = OutputPath("OutboundInspectionResult", ".xlsx"); OutboundInspectionXlsxWriter.Write(path, _outboundRecords); await ShareOutputAsync(path); }); }
    public async void UploadPallet_Clicked(object? sender, EventArgs e)
    {
        await RunAsync(async () =>
        {
            string palletNumber = (_view.PalletNumberEntry.Text ?? string.Empty).Trim();
            string apiKey = Preferences.Default.Get("UploadApiKey", string.Empty);
            ShelvedPalletUploadResult result = await _palletUpload.UploadAsync(palletNumber, _outboundRecords, apiKey);
            _view.OutboundStatusLabel.Text = L.Format("UploadShelvedPalletSuccess", result.InsertedCount, result.PalletNumber, result.ShelvedAt);
        });
    }
    public async void PickFeeTemplate_Clicked(object? sender, EventArgs e) { await RunAsync(async () => { _feeTemplate = await PickAndCacheAsync(L.Get("SelectFeeTemplateTitle")); _view.FeeTemplateLabel.Text = _feeTemplate ?? L.Get("OperationsNotSelected"); }); }
    public async void ExportFee_Clicked(object? sender, EventArgs e)
    {
        await RunAsync(async () => { if (_feeTemplate is null || _baseData.Records.Count == 0) throw new InvalidOperationException(L.Get("OperationsNeedFeeSources")); string path = OutputPath("LocationFeeComparisonResult", ".xlsx"); var summary = LocationFeeComparisonService.Build(_feeTemplate, _baseData.Records, path); _view.FeeStatusLabel.Text = L.Format("FeeSummary", summary.SheetCount, summary.RowCount, summary.MatchedCount, summary.UnmatchedCount); await ShareOutputAsync(path); });
    }

    private void RefreshInboundStatus() => _view.InboundStatusLabel.Text = L.Format("OperationsInboundSummary", _inboundRecords.Count, _serialNumbers.Count);
    private static async Task<string?> PickAndCacheAsync(string title) { var result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = title }); if (result is null) return null; string path = Path.Combine(FileSystem.CacheDirectory, $"{Guid.NewGuid():N}_{result.FileName}"); await using var input = await result.OpenReadAsync(); await using var output = File.Create(path); await input.CopyToAsync(output); return path; }
    private static string OutputPath(string name, string extension) => Path.Combine(FileSystem.CacheDirectory, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}");
    private static Task ShareOutputAsync(string path) => Share.Default.RequestAsync(new ShareFileRequest { Title = L.Get("OperationsShareTitle"), File = new ShareFile(path) });
    private async Task RunAsync(Func<Task> action) { try { _view.IsBusy = true; await action(); } catch (Exception ex) { await _window.ShowAlertAsync(L.Get("ProcessingFailed"), ex.Message, L.Get("Confirm")); } finally { _view.IsBusy = false; } }
    private static bool TryDate(string? value, out DateTime date) { string text = (value ?? "").Trim(); if (DateTime.TryParse(text, out date)) { date = date.AddHours(BeijingToNewJerseyHourOffset); return true; } if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double serial) && serial > 0) { try { date = DateTime.FromOADate(serial).AddHours(BeijingToNewJerseyHourOffset); return true; } catch { } } date = default; return false; }
    private static string Clean(string? value) => (value ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
    private static string Csv(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";

}
