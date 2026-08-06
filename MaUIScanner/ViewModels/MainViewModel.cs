using MaUIScanner.Models;
using MaUIScanner.Services;
using System.Windows.Input;

namespace MaUIScanner.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ScanService _scan;
    private readonly MeterModelService _models;
    private readonly SpeechService _speech;
    private readonly WorkOrderSearchService _search;
    private readonly CsvImportService _csv;
    private readonly ScanLogService _log;
    private readonly ILabelPrinter _printer;
    private readonly LocalizationService _text;
    private string _scanCode = string.Empty;
    private string _status = string.Empty;
    private bool _autoPrint = true;
    private bool _busy;
    private Color _statusColor = Colors.DarkGreen;
    public MainViewModel(ScanService scan, MeterModelService models, SpeechService speech, WorkOrderSearchService search, CsvImportService csv, ScanLogService log, ILabelPrinter printer, LocalizationService text)
    {
        _scan = scan;
        _models = models;
        _speech = speech;
        _search = search;
        _csv = csv;
        _log = log;
        _printer = printer;
        _text = text;
        ProcessCommand = new Command(async () => await ProcessAsync(), () => !Busy);
        ImportCommand = new Command(async () => await ImportAsync(), () => !Busy);
        OpenLogCommand = new Command(async () => await OpenLogAsync(), () => !Busy);
        ChangeLanguageCommand = new Command<string>(ChangeLanguage);
        _text.Changed += (_, _) => RefreshText();
        RefreshText();
    }
    public event EventHandler? FocusRequested;
    public ICommand ProcessCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand OpenLogCommand { get; }
    public ICommand ChangeLanguageCommand { get; }
    public string ScanCode { get => _scanCode; set => SetProperty(ref _scanCode, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public bool AutoPrint { get => _autoPrint; set => SetProperty(ref _autoPrint, value); }
    public bool Busy { get => _busy; private set { if (SetProperty(ref _busy, value)) RefreshCommands(); } }
    public Color StatusColor { get => _statusColor; private set => SetProperty(ref _statusColor, value); }
    public string Title => _text.Get("Title");
    public string ScanTitle => _text.Get("Scan");
    public string Hint => _text.Get("Hint");
    public string AutoPrintText => _text.Get("AutoPrint");
    public string PrintText => _text.Get("Print");
    public string OpenLogText => _text.Get("OpenLog");
    public string ImportText => _text.Get("Import");
    public string RuntimeText => _text.Get("Runtime");
    public string StatusTitle => _text.Get("Status");
    public string OfflineText => _text.Get("Offline");
    public string PrinterText
    {
        get
        {
            try { return _text.Format("Printer", _printer.GetDefaultPrinterName()); }
            catch { return _text.Format("Printer", "—"); }
        }
    }
    public string CountText => _text.Format("Count", _scan.Count);
    public string LogText => _scan.LogPath;
    private async Task ProcessAsync()
    {
        string code = ScanCode.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            SetStatus("扫描内容不能为空。", true);
            RequestFocus();
            return;
        }
        Busy = true;
        try
        {
            await Clipboard.Default.SetTextAsync(code);
            ScanResult result = await _scan.RecordAsync(code);
            Notify(nameof(CountText));
            WorkOrderRemark? matched = _search.Find(result.Code);
            string printCode = string.IsNullOrWhiteSpace(matched?.Sn) ? result.Code : matched.Sn.Trim();
            string model = _models.Find(result.Code);
            if (result.Oid.IsOid) await _speech.SpeakOidAsync();
            if (result.Oid.IsOid && !result.Oid.ShouldPrint) SetStatus(_text.Get("OidFirst"), false);
            else if (result.Oid.ShouldPrint)
            {
                await _printer.PrintAsync(printCode, 1, matched, model);
                SetStatus(_text.Format("OidPrint", printCode), false);
            }
            else if (AutoPrint)
            {
                await _printer.PrintAsync(printCode, 2, matched, model);
                await _speech.SpeakTailAsync(printCode);
                SetStatus(_text.Format("Printed", printCode), false);
            }
            else SetStatus(_text.Format("Saved", result.Code), false);
            ScanCode = string.Empty;
        }
        catch (Exception exception)
        {
            await _log.WriteErrorAsync(exception);
            SetStatus(exception.Message, true);
        }
        finally
        {
            Busy = false;
            RequestFocus();
        }
    }
    private async Task ImportAsync()
    {
        try
        {
            FilePickerFileType csvType = new(new Dictionary<DevicePlatform, IEnumerable<string>> { [DevicePlatform.WinUI] = new[] { ".csv" }, [DevicePlatform.Android] = new[] { "text/csv", "text/comma-separated-values" }, [DevicePlatform.iOS] = new[] { "public.comma-separated-values-text" }, [DevicePlatform.MacCatalyst] = new[] { "public.comma-separated-values-text" } });
            FileResult? file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = _text.Get("Import"), FileTypes = csvType });
            if (file == null) return;
            await using Stream stream = await file.OpenReadAsync();
            IReadOnlyList<WorkOrderRemark> items = await _csv.ImportAsync(stream);
            _search.Merge(items);
            SetStatus($"CSV：{items.Count}", false);
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, true);
        }
        RequestFocus();
    }
    private async Task OpenLogAsync()
    {
        try { await _log.OpenAsync(); }
        catch (Exception exception) { SetStatus(exception.Message, true); }
        RequestFocus();
    }
    private void ChangeLanguage(string language) => _text.Change(language);
    private void RefreshText()
    {
        Notify(nameof(Title));
        Notify(nameof(ScanTitle));
        Notify(nameof(Hint));
        Notify(nameof(AutoPrintText));
        Notify(nameof(PrintText));
        Notify(nameof(OpenLogText));
        Notify(nameof(ImportText));
        Notify(nameof(RuntimeText));
        Notify(nameof(StatusTitle));
        Notify(nameof(OfflineText));
        Notify(nameof(PrinterText));
        Notify(nameof(CountText));
        Status = _text.Get("Waiting");
    }
    private void RefreshCommands()
    {
        ((Command)ProcessCommand).ChangeCanExecute();
        ((Command)ImportCommand).ChangeCanExecute();
        ((Command)OpenLogCommand).ChangeCanExecute();
    }
    private void SetStatus(string message, bool error)
    {
        Status = message;
        StatusColor = error ? Colors.DarkRed : Colors.DarkGreen;
    }
    private void RequestFocus() => FocusRequested?.Invoke(this, EventArgs.Empty);
}
