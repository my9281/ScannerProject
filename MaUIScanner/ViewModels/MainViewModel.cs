using MaUIScanner.Models;
using MaUIScanner.Services;
using System.Windows.Input;

namespace MaUIScanner.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ScanService _scan; private readonly MeterModelService _models; private readonly SpeechService _speech;
    private readonly ScanLogService _log; private readonly ILabelPrinter _printer; private readonly ScanUploadService _upload;
    private readonly WorkOrderSearchService _workOrders; private readonly WorkOrderRemarkApiService _api; private readonly CsvImportService _csv; private readonly SessionStore _sessions;
    private string _scanCode = string.Empty, _status = "等待扫描", _lastRefresh = "尚未刷新"; private bool _busy; private Color _statusColor = Color.FromArgb("#2457D6"); private int _printCopies;

    public MainViewModel(ScanService scan, MeterModelService models, SpeechService speech, ScanLogService log, ILabelPrinter printer,
        ScanUploadService upload, WorkOrderSearchService workOrders, WorkOrderRemarkApiService api, CsvImportService csv, SessionStore sessions)
    {
        _scan=scan; _models=models; _speech=speech; _log=log; _printer=printer; _upload=upload; _workOrders=workOrders; _api=api; _csv=csv; _sessions=sessions;
        _printCopies = Preferences.Default.Get("PrintCopies", 0);
        ProcessCommand=new Command(async()=>await ProcessAsync(),()=>!Busy); OpenLogCommand=new Command(async()=>await OpenLogAsync(),()=>!Busy);
        UploadCommand=new Command(async()=>await UploadAsync(),()=>!Busy); ImportCommand=new Command(async()=>await ImportAsync(),()=>!Busy);
        RefreshCommand=new Command(async()=>await RefreshAsync(true),()=>!Busy); LogoutCommand=new Command(async()=>await LogoutAsync(),()=>!Busy);
    }
    public event EventHandler? FocusRequested; public event EventHandler? LogoutRequested;
    public ICommand ProcessCommand { get; } public ICommand OpenLogCommand { get; } public ICommand UploadCommand { get; }
    public ICommand ImportCommand { get; } public ICommand RefreshCommand { get; } public ICommand LogoutCommand { get; }
    public string ScanCode { get=>_scanCode; set=>SetProperty(ref _scanCode,value); }
    public string Status { get=>_status; private set=>SetProperty(ref _status,value); }
    public Color StatusColor { get=>_statusColor; private set=>SetProperty(ref _statusColor,value); }
    public string OperatorText => $"{(_sessions.Current?.Operator ?? "本地扫描")} · {(_sessions.Current?.Role ?? "本地模式")}";
    public string CountText => $"本次扫描：{_scan.Count:N0}"; public string LogText => $"日志：{_log.FilePath}";
    public string ServiceText => _sessions.Current?.IsLocalMode == true ? "本地离线模式" : $"在线工单：{_workOrders.Count:N0} 条";
    public string LastRefresh { get=>_lastRefresh; private set=>SetProperty(ref _lastRefresh,value); }
    public bool Busy { get=>_busy; private set { if(SetProperty(ref _busy,value)) RefreshCommands(); } }
    public int PrintCopies { get=>_printCopies; set { if(SetProperty(ref _printCopies,value)) Preferences.Default.Set("PrintCopies",value); } }

    public async Task InitializeAsync() { Notify(nameof(OperatorText)); Notify(nameof(ServiceText)); if(_sessions.Current?.IsLocalMode!=true) await RefreshAsync(false); RequestFocus(); }
    private async Task ProcessAsync()
    {
        string code=ScanCode.Trim(); if(string.IsNullOrWhiteSpace(code)){SetStatus("扫描内容不能为空。",true);RequestFocus();return;}
        if(ScanService.IsGs1AreaCode(code)){await _speech.SpeakGs1AreaWarningAsync();SetStatus("警告：扫描到 GS1 AI 420 地区码，本次不记录。",true);ScanCode=string.Empty;RequestFocus();return;}
        Busy=true;
        try
        {
            ScanResult result=await _scan.RecordAsync(code); WorkOrderRemark? workOrder=_workOrders.Find(result.Code); string model=_models.Find(result.Code);
            if(result.Oid.IsOid) await _speech.SpeakOidAsync(); int copies=_printer.IsSupported ? PrintCopies : 0;
            if(result.Oid.IsOid&&!result.Oid.ShouldPrint) SetStatus($"已记录 OID：{result.Code}，首次不打印。",false);
            else if(copies>0){await _printer.PrintAsync(result.Code,copies,workOrder,model);await _speech.SpeakTailAsync(result.Code);SetStatus($"已记录并打印 {copies} 份：{result.Code}",false);}
            else {if(!result.Oid.IsOid) await _speech.SpeakTailAsync(result.Code);SetStatus(result.WasRecorded?$"已记录：{result.Code}":$"记录已存在：{result.Code}",false);}
            ScanCode=string.Empty; Notify(nameof(CountText));
        }
        catch(Exception ex){await _log.WriteErrorAsync(ex);SetStatus(ex.Message,true);} finally{Busy=false;RequestFocus();}
    }
    private async Task RefreshAsync(bool showResult)
    {
        if(_sessions.Current?.IsLocalMode==true){if(showResult)SetStatus("本地模式不连接在线工单。",false);return;}
        Busy=true; try{WorkOrderRemarkResponse response=await _api.GetAsync(_sessions.Current?.Token??string.Empty);_workOrders.Replace(response.WorkOrders);LastRefresh=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");Notify(nameof(ServiceText));if(showResult)SetStatus($"已刷新 {response.ReturnedCount:N0} 条工单。",false);}catch(Exception ex){SetStatus("刷新失败："+ex.Message,true);}finally{Busy=false;}
    }
    private async Task ImportAsync(){FileResult? file=await FilePicker.Default.PickAsync(new PickOptions{PickerTitle="选择紧急工单 CSV"});if(file==null)return;Busy=true;try{await using Stream stream=await file.OpenReadAsync();IReadOnlyList<WorkOrderRemark> items=await _csv.ImportAsync(stream);_workOrders.Merge(items);Notify(nameof(ServiceText));SetStatus($"已导入 {items.Count:N0} 条紧急规则。",false);}catch(Exception ex){SetStatus("导入失败："+ex.Message,true);}finally{Busy=false;}}
    private async Task OpenLogAsync(){try{await _log.OpenAsync();}catch(Exception ex){SetStatus("打开日志失败："+ex.Message,true);}}
    private async Task UploadAsync(){Busy=true;try{ScanUploadResult result=await _upload.UploadAsync(_log.FilePath);SetStatus("上传成功："+result.FileName,false);}catch(Exception ex){SetStatus("上传失败："+ex.Message,true);}finally{Busy=false;}}
    private async Task LogoutAsync(){await _sessions.ClearSessionAsync();LogoutRequested?.Invoke(this,EventArgs.Empty);}
    private void SetStatus(string text,bool error){Status=text;StatusColor=Color.FromArgb(error?"#C43D4B":"#25805A");}
    private void RefreshCommands(){foreach(Command command in new[]{(Command)ProcessCommand,(Command)OpenLogCommand,(Command)UploadCommand,(Command)ImportCommand,(Command)RefreshCommand,(Command)LogoutCommand})command.ChangeCanExecute();}
    private void RequestFocus()=>FocusRequested?.Invoke(this,EventArgs.Empty);
}
