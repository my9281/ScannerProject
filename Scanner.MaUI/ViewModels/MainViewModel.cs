using Scanner.MaUI.Services;
using Scanner.Models.MauiContracts;
using System.Windows.Input;

namespace Scanner.MaUI.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly ScanService _scan; private readonly MeterModelService _models; private readonly SpeechService _speech;
    private readonly ScanLogService _log; private readonly ILabelPrinter _printer; private readonly ScanUploadService _upload;
    private readonly WorkOrderSearchService _workOrders; private readonly WorkOrderRemarkApiService _api; private readonly CsvImportService _csv; private readonly SessionStore _sessions;
    private static LocalizationService L => LocalizationService.Current;
    private string _scanCode = string.Empty, _status = string.Empty, _lastRefresh = string.Empty, _paperSize = "4 × 6"; private bool _busy; private Color _statusColor = Color.FromArgb("#2457D6"); private int _printCopies;

    public MainViewModel(ScanService scan, MeterModelService models, SpeechService speech, ScanLogService log, ILabelPrinter printer,
        ScanUploadService upload, WorkOrderSearchService workOrders, WorkOrderRemarkApiService api, CsvImportService csv, SessionStore sessions)
    {
        _scan = scan; _models = models; _speech = speech; _log = log; _printer = printer; _upload = upload; _workOrders = workOrders; _api = api; _csv = csv; _sessions = sessions;
        _printCopies = Preferences.Default.Get("PrintCopies", 0);
        _paperSize = Preferences.Default.Get("LabelPaperSize", "4 × 6");
        RefreshLocalizedText();
        ProcessCommand = new Command(async () => await ProcessAsync(), () => !Busy); OpenLogCommand = new Command(async () => await OpenLogAsync(), () => !Busy);
        UploadCommand = new Command(async () => await UploadAsync(), () => !Busy); ImportCommand = new Command(async () => await ImportAsync(), () => !Busy);
        RefreshCommand = new Command(async () => await RefreshAsync(true), () => !Busy); LogoutCommand = new Command(async () => await LogoutAsync(), () => !Busy);
    }
    public event EventHandler? FocusRequested; public event EventHandler? LogoutRequested;
    public Func<IReadOnlyList<string>, Task<string?>>? ModelSelectionRequested { get; set; }
    public ICommand ProcessCommand { get; }
    public ICommand OpenLogCommand { get; }
    public ICommand UploadCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand LogoutCommand { get; }
    public string ScanCode { get => _scanCode; set => SetProperty(ref _scanCode, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public Color StatusColor { get => _statusColor; private set => SetProperty(ref _statusColor, value); }
    public string OperatorText => _sessions.Current?.IsLocalMode == true ? L.Get("LocalScanningMode") : $"{_sessions.Current?.Operator} · {_sessions.Current?.Role}";
    public string CountText => L.Format("MobileCount", _scan.Count); public string LogText => L.Format("MobileLog", _log.FilePath);
    public string ServiceText => _sessions.Current?.IsLocalMode == true ? L.Get("LocalScanningMode") : L.Format("MobileOnlineOrders", _workOrders.Count);
    public string LastRefresh { get => _lastRefresh; private set { if (SetProperty(ref _lastRefresh, value)) Notify(nameof(LastRefreshText)); } }
    public string LastRefreshText => L.Format("LastRefreshFormat", string.IsNullOrEmpty(LastRefresh) ? L.Get("NeverRefreshed") : LastRefresh);
    public bool Busy { get => _busy; private set { if (SetProperty(ref _busy, value)) RefreshCommands(); } }
    public int PrintCopies { get => _printCopies; set { if (value >= 0 && value <= 2 && SetProperty(ref _printCopies, value)) Preferences.Default.Set("PrintCopies", value); } }
    public IReadOnlyList<string> PaperSizes { get; } = new[] { "4 × 6", "4 × 4" };
    public string PaperSize { get => _paperSize; set { if (SetProperty(ref _paperSize, value)) Preferences.Default.Set("LabelPaperSize", value); } }

    public async Task InitializeAsync() { Notify(nameof(OperatorText)); Notify(nameof(ServiceText)); if (_sessions.Current?.IsLocalMode != true) await RefreshAsync(false); RequestFocus(); }
    private async Task ProcessAsync()
    {
        string code = ScanCode.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            SetStatus(L.Get("EmptyCode"), true);
            RequestFocus();
            return;
        }
        if (ScanService.IsGs1AreaCode(code))
        {
            await _speech.SpeakGs1AreaWarningAsync();
            SetStatus(L.Get("Gs1AreaWarning"), true);
            ScanCode = string.Empty; RequestFocus();
            return;
        }
        Busy = true;
        try
        {
            ScanResult result = await _scan.RecordAsync(code);
            WorkOrderMatch? match = _workOrders.Resolve(result.Code, result.Oid.IsOid);
            WorkOrderRemark? workOrder = match?.WorkOrder;
            string printCode = match?.GetPrintCode(result.Code) ?? result.Code;
            string model = _models.Find(printCode);
            if (result.Oid.IsOid) await _speech.SpeakOidAsync();
            int copies = _printer.IsSupported ? PrintCopies : 0;
            if (result.Oid.IsOid && !result.Oid.ShouldPrint) SetStatus(L.Format("MobileOidFirst", result.Code), false);
            else if (copies > 0)
            {
                if (string.IsNullOrWhiteSpace(model) && ModelSelectionRequested is not null) model = await ModelSelectionRequested(_models.GetModels()) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(model))
                {
                    SetStatus($"未选择型号：{printCode}", true); return;
                }
                await _printer.PrintAsync(printCode, copies, workOrder, model, PaperSize);
                await _speech.SpeakTailAsync(printCode);
                SetStatus(L.Format("MobilePrinted", copies, printCode), false);
            }
            else
            {
                if (!result.Oid.IsOid) await _speech.SpeakTailAsync(result.Code);
                SetStatus(result.WasRecorded ? L.Format("MobileRecorded", result.Code) : L.Format("MobileDuplicate", result.Code), false);
            }
            ScanCode = string.Empty;
            Notify(nameof(CountText));
        }
        catch (Exception ex)
        {
            await _log.WriteErrorAsync(ex);
            SetStatus(ex.Message, true);
        }
        finally
        {
            Busy = false;
            RequestFocus();
        }
    }
    private async Task RefreshAsync(bool showResult)
    {
        if (_sessions.Current?.IsLocalMode == true) { if (showResult) SetStatus(L.Get("LocalModeReady"), false); return; }
        Busy = true; try { WorkOrderRemarkResponse response = await _api.GetAsync(_sessions.Current?.Token ?? string.Empty); _workOrders.Replace(response.WorkOrders); LastRefresh = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); Notify(nameof(ServiceText)); if (showResult) SetStatus(L.Format("MobileOrdersRefreshed", response.ReturnedCount), false); } catch (Exception ex) { SetStatus(L.Get("RefreshFailedPrefix") + ex.Message, true); } finally { Busy = false; }
    }
    private async Task ImportAsync() { FileResult? file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = L.Get("SelectUrgentCsv") }); if (file == null) return; Busy = true; try { IReadOnlyList<WorkOrderRemark> items = await _csv.ImportAsync(file); _workOrders.Merge(items); Notify(nameof(ServiceText)); SetStatus(L.Format("MobileImported", items.Count), false); } catch (Exception ex) { SetStatus(L.Get("ImportFailedPrefix") + ex.Message, true); } finally { Busy = false; } }
    private async Task OpenLogAsync() { try { await _log.OpenAsync(); } catch (Exception ex) { SetStatus(L.Format("OpenLogFailed", ex.Message), true); } }
    private async Task UploadAsync() { Busy = true; try { ScanUploadResult result = await _upload.UploadAsync(_log.FilePath); SetStatus(L.Format("UploadScanLogSuccess", result.FileName), false); } catch (Exception ex) { SetStatus(L.Format("UploadScanLogFailed", ex.Message), true); } finally { Busy = false; } }
    private async Task LogoutAsync() { await _sessions.ClearSessionAsync(); LogoutRequested?.Invoke(this, EventArgs.Empty); }
    private void SetStatus(string text, bool error) { Status = text; StatusColor = Color.FromArgb(error ? "#C43D4B" : "#25805A"); }
    private void RefreshCommands() { foreach (Command command in new[] { (Command)ProcessCommand, (Command)OpenLogCommand, (Command)UploadCommand, (Command)ImportCommand, (Command)RefreshCommand, (Command)LogoutCommand }) command.ChangeCanExecute(); }
    public void RefreshLocalizedText()
    {
        foreach (string property in new[] { nameof(OperatorText), nameof(CountText), nameof(LogText), nameof(ServiceText), nameof(LastRefreshText), nameof(PrintCopies) }) Notify(property);
        SetStatus(L.Get("WaitingForScan"), false);
    }
    private void RequestFocus() => FocusRequested?.Invoke(this, EventArgs.Empty);
}
