using MaUIScanner.Models;
using MaUIScanner.Services;
using System.Windows.Input;

namespace MaUIScanner.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ScanService _scan;
    private readonly MeterModelService _models;
    private readonly SpeechService _speech;
    private readonly ScanLogService _log;
    private readonly ILabelPrinter _printer;
    private readonly ScanUploadService _upload;
    private string _scanCode = string.Empty;
    private string _scanHint = "扫描后按 Enter 处理";
    private bool _busy;
    private Color _hintColor = Colors.DimGray;
    public MainViewModel(ScanService scan, MeterModelService models, SpeechService speech, ScanLogService log, ILabelPrinter printer, ScanUploadService upload)
    {
        _scan = scan;
        _models = models;
        _speech = speech;
        _log = log;
        _printer = printer;
        _upload = upload;
        ProcessCommand = new Command(async () => await ProcessAsync(), () => !Busy);
        ExportCommand = new Command(async () => await ExportAsync(), () => !Busy);
    }
    public event EventHandler? FocusRequested;
    public ICommand ProcessCommand { get; }
    public ICommand ExportCommand { get; }
    public string ScanCode { get => _scanCode; set => SetProperty(ref _scanCode, value); }
    public string ScanHint { get => _scanHint; private set => SetProperty(ref _scanHint, value); }
    public Color HintColor { get => _hintColor; private set => SetProperty(ref _hintColor, value); }
    public bool Busy { get => _busy; private set { if (SetProperty(ref _busy, value)) RefreshCommands(); } }
    private async Task ProcessAsync()
    {
        string code = ScanCode.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            SetHint("扫描内容不能为空。", true);
            RequestFocus();
            return;
        }
        if (ScanService.IsGs1AreaCode(code))
        {
            await _speech.SpeakGs1AreaWarningAsync();
            SetHint("警告：扫描到 GS1 AI 420 地区码，本次不记录、不打印。", true);
            ScanCode = string.Empty;
            RequestFocus();
            return;
        }
        Busy = true;
        try
        {
            ScanResult result = await _scan.RecordAsync(code);
            string model = _models.Find(result.Code);
            if (result.Oid.IsOid) await _speech.SpeakOidAsync();
            if (!_printer.IsSupported)
            {
                if (!result.Oid.IsOid) await _speech.SpeakTailAsync(result.Code);
                SetHint(result.WasRecorded ? $"已记录：{result.Code}" : $"扫描完成，记录已存在：{result.Code}", false);
            }
            else if (result.Oid.IsOid && !result.Oid.ShouldPrint) SetHint($"已记录 OID：{result.Code}，本次不打印。", false);
            else if (result.Oid.ShouldPrint)
            {
                await _printer.PrintAsync(result.Code, 1, null, model);
                SetHint($"OID 已打印：{result.Code}", false);
            }
            else
            {
                await _printer.PrintAsync(result.Code, 2, null, model);
                await _speech.SpeakTailAsync(result.Code);
                SetHint(result.WasRecorded ? $"已记录并打印：{result.Code}" : $"已打印，扫描记录未重复写入：{result.Code}", false);
            }
            ScanCode = string.Empty;
        }
        catch (Exception exception)
        {
            await _log.WriteErrorAsync(exception);
            SetHint(exception.Message, true);
        }
        finally
        {
            Busy = false;
            RequestFocus();
        }
    }
    private async Task ExportAsync()
    {
        Busy = true;
        SetHint("正在上传扫描记录…", false);
        try
        {
            ScanUploadResult result = await _upload.UploadAsync(_log.FilePath);
            SetHint($"上传成功：{result.FileName}", false);
        }
        catch (Exception exception)
        {
            await _log.WriteErrorAsync(exception);
            SetHint("上传失败：" + exception.Message, true);
        }
        finally
        {
            Busy = false;
            RequestFocus();
        }
    }
    private void RefreshCommands()
    {
        ((Command)ProcessCommand).ChangeCanExecute();
        ((Command)ExportCommand).ChangeCanExecute();
    }
    private void SetHint(string message, bool error)
    {
        ScanHint = message;
        HintColor = error ? Colors.DarkRed : Colors.DarkGreen;
    }
    private void RequestFocus() => FocusRequested?.Invoke(this, EventArgs.Empty);
}
