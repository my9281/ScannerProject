using Scanner.Helpers;
using Scanner.Models;
using Scanner.Services;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Scanner.ViewModels
{
    public sealed class MainWindowViewModel : ViewModelBase
    {
        private readonly NetworkHelper _network;
        private readonly PrintingHelper _printing;
        private readonly ScanService _scanning;
        private readonly ScanUploadService _upload;
        private readonly MeterModelService _meterModels;
        private readonly SpeechService _speech;
        private readonly WorkOrderSearchService _search;
        private readonly UrgentWorkOrderImportHelper _workOrderImport;
        private readonly DialogHelper _dialogs;
        private string _scanCode;
        private string _statusText;
        private Brush _statusBrush = Brushes.DarkGreen;
        private string _printerText;
        private string _webStatus;
        private string _webLastTime;
        private string _labelPaperSize;
        private int _printCopies;
        private bool _isBusy;
        public MainWindowViewModel() : this(new NetworkHelper(), new PrintingHelper(), new ScanService(), new ScanUploadService(), new MeterModelService(), new SpeechService(), new WorkOrderSearchService(), new UrgentWorkOrderImportHelper(), new DialogHelper())
        {
        }

        public MainWindowViewModel(NetworkHelper network, PrintingHelper printing, ScanService scanning, ScanUploadService upload, MeterModelService meterModels, SpeechService speech, WorkOrderSearchService search, UrgentWorkOrderImportHelper workOrderImport, DialogHelper dialogs)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _printing = printing ?? throw new ArgumentNullException(nameof(printing));
            _scanning = scanning ?? throw new ArgumentNullException(nameof(scanning));
            _upload = upload ?? throw new ArgumentNullException(nameof(upload));
            _meterModels = meterModels ?? throw new ArgumentNullException(nameof(meterModels));
            _speech = speech ?? throw new ArgumentNullException(nameof(speech));
            _search = search ?? throw new ArgumentNullException(nameof(search));
            _workOrderImport = workOrderImport ?? throw new ArgumentNullException(nameof(workOrderImport));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _labelPaperSize = PrintingHelper.NormalizePaperSize(Properties.Settings.Default.LabelPaperSize);
            _printCopies = NormalizePrintCopies(Properties.Settings.Default.PrintCopies);
            ProcessScanCommand = new RelayCommand(ProcessScan, () => !IsBusy);
            OpenLogCommand = new RelayCommand(OpenLog, () => !IsBusy);
            UploadLogCommand = new RelayCommand(async () => await UploadLogAsync(), () => !IsBusy);
            RefreshCommand = new RelayCommand(async () => await RefreshWorkOrdersAsync(true), () => !IsBusy);
            ImportUrgentWorkOrdersCommand = new RelayCommand(ImportUrgentWorkOrders, () => !IsBusy);
            ChangeLanguageCommand = new RelayCommand(parameter => ChangeLanguage(parameter as string));
            RefreshLocalizedText();
        }

        public event EventHandler FocusRequested;
        public ICommand ProcessScanCommand { get; }
        public ICommand OpenLogCommand { get; }
        public ICommand UploadLogCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ImportUrgentWorkOrdersCommand { get; }
        public ICommand ChangeLanguageCommand { get; }
        public string ScanCode { get => _scanCode; set => SetProperty(ref _scanCode, value); }

        public int PrintCopies
        {
            get => _printCopies;
            set
            {
                int normalized = NormalizePrintCopies(value);
                if (!SetProperty(ref _printCopies, normalized))
                {
                    return;
                }
                RaisePropertyChanged(nameof(IsPrintDisabled));
                RaisePropertyChanged(nameof(IsPrintOnce));
                RaisePropertyChanged(nameof(IsPrintTwice));
                RaisePropertyChanged(nameof(CopiesInformationText));
                try
                {
                    Properties.Settings.Default.PrintCopies = normalized;
                    Properties.Settings.Default.Save();
                }
                catch (Exception ex)
                {
                    _scanning.WriteError(ex);
                    SetStatus(FormatResource("SettingsSaveFailed", ex.Message), true);
                }
            }
        }

        public bool IsPrintDisabled
        {
            get => PrintCopies == 0;
            set { if (value) PrintCopies = 0; }
        }

        public bool IsPrintOnce
        {
            get => PrintCopies == 1;
            set { if (value) PrintCopies = 1; }
        }

        public bool IsPrintTwice
        {
            get => PrintCopies == 2;
            set { if (value) PrintCopies = 2; }
        }

        public string LabelPaperSize
        {
            get => _labelPaperSize;
            set
            {
                string normalized = PrintingHelper.NormalizePaperSize(value);
                if (!SetProperty(ref _labelPaperSize, normalized))
                {
                    return;
                }
                RaisePropertyChanged(nameof(IsWideLabelPaper));
                RaisePropertyChanged(nameof(IsSquareLabelPaper));
                RaisePropertyChanged(nameof(LabelInformationText));
                try
                {
                    Properties.Settings.Default.LabelPaperSize = normalized;
                    Properties.Settings.Default.Save();
                }
                catch (Exception ex)
                {
                    _scanning.WriteError(ex);
                    SetStatus(FormatResource("SettingsSaveFailed", ex.Message), true);
                }
            }
        }

        public bool IsWideLabelPaper
        {
            get => LabelPaperSize == PrintingHelper.DefaultPaperSize;
            set
            {
                if (value)
                {
                    LabelPaperSize = PrintingHelper.DefaultPaperSize;
                }
            }
        }

        public bool IsSquareLabelPaper
        {
            get => LabelPaperSize == PrintingHelper.SquarePaperSize;
            set
            {
                if (value)
                {
                    LabelPaperSize = PrintingHelper.SquarePaperSize;
                }
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    RaisePropertyChanged(nameof(IsInputEnabled));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IsInputEnabled => !IsBusy;
        public string OperatorText => App.CurrentSession == null ? string.Empty : App.CurrentSession.IsLocalMode ? Resource("LocalScanningMode") : App.CurrentSession.Operator + " / " + App.CurrentSession.Role;
        public string LogFileText => FormatResource("LogFileValue", _scanning.LogFilePath);
        public string CountText => FormatResource("ScanCount", _scanning.ScanCount);
        public string LabelInformationText => FormatResource("LabelInformation", LabelPaperSize == PrintingHelper.SquarePaperSize ? "4 × 4" : "4 × 6");
        public string CopiesInformationText => FormatResource("CopiesInformation", Resource(PrintCopies == 0 ? "PrintNone" : PrintCopies == 1 ? "PrintOnce" : "PrintTwice"));
        public string PrinterText { get => _printerText; private set => SetProperty(ref _printerText, value); }
        public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
        public Brush StatusBrush { get => _statusBrush; private set => SetProperty(ref _statusBrush, value); }
        public string WebStatus { get => _webStatus; private set => SetProperty(ref _webStatus, value); }
        public string WebLastTime { get => _webLastTime; private set => SetProperty(ref _webLastTime, value); }

        public async Task InitializeAsync()
        {
            RefreshLocalizedText();
            await RefreshWorkOrdersAsync(false);
            RequestFocus();
        }

        public void RefreshLocalizedText()
        {
            try
            {
                PrinterText = FormatResource("DefaultPrinter", _printing.GetDefaultPrinterName());
            }
            catch
            {
                PrinterText = Resource("PrinterNotFound");
            }
            RaisePropertyChanged(nameof(LogFileText));
            RaisePropertyChanged(nameof(OperatorText));
            RaisePropertyChanged(nameof(CountText));
            RaisePropertyChanged(nameof(LabelInformationText));
            RaisePropertyChanged(nameof(CopiesInformationText));
            SetStatus(Resource("WaitingForScan"), false);
        }

        private void ProcessScan()
        {
            string code = (ScanCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                SetStatus(Resource("EmptyCode"), true);
                ScanCode = string.Empty;
                RequestFocus();
                return;
            }
            if (ScanService.IsGs1AreaCode(code))
            {
                _speech.SpeakGs1AreaWarning();
                SetStatus(Resource("Gs1AreaWarning"), true);
                ScanCode = string.Empty;
                RequestFocus();
                return;
            }
            IsBusy = true;
            try
            {
                TryCopy(code);
                SetStatus(FormatResource("Processing", code), false);
                ScanResult scan = _scanning.Record(code);
                code = scan.Code;
                if (scan.WasRecorded)
                {
                    RaisePropertyChanged(nameof(CountText));
                }
                WorkOrderMatch match = _search.Resolve(code, scan.Oid.IsOid);
                WorkOrderRemark matched = match == null ? null : match.WorkOrder;
                string printCode = match == null ? code : match.GetPrintCode(code);
                string meterModel = _meterModels.FindModel(printCode);
                if (scan.Oid.IsOid && !scan.Oid.ShouldPrint)
                {
                    SetStatus(Resource("OidRecorded"), false);
                }
                else if (PrintCopies > 0)
                {
                    if (string.IsNullOrWhiteSpace(meterModel))
                    {
                        meterModel = _dialogs.SelectMeterModel(_meterModels);
                        if (string.IsNullOrWhiteSpace(meterModel))
                        {
                            SetStatus(FormatResource("ModelSelectionCancelled", printCode), true);
                            ScanCode = string.Empty;
                            return;
                        }
                    }
                    _printing.PrintLabel(printCode, PrintCopies, matched, meterModel, LabelPaperSize);
                    if (scan.Oid.ShouldPrint)
                    {
                        SetStatus(FormatResource("OidReprinted", PrintCopies, printCode), false);
                    }
                    else
                    {
                        _speech.SpeakChineseTail(printCode);
                        SetStatus(matched != null && matched.IsUrgent ? FormatResource("UrgentPrinted", PrintCopies, printCode) : FormatResource("SavedAndPrinted", PrintCopies, printCode), false);
                    }
                }
                else
                {
                    SetStatus(FormatResource("SavedWithoutPrint", code), false);
                }
                ScanCode = string.Empty;
            }
            catch (Exception ex)
            {
                _scanning.WriteError(ex);
                SetStatus(FormatResource("ProcessFailed", ex.Message), true);
            }
            finally
            {
                IsBusy = false;
                RequestFocus();
            }
        }

        private async Task RefreshWorkOrdersAsync(bool showResult)
        {
            if (App.CurrentSession != null && App.CurrentSession.IsLocalMode)
            {
                WebStatus = Resource("WebLocalMode");
                WebLastTime = "--";
                SetStatus(Resource("LocalModeReady"), false);
                RequestFocus();
                return;
            }
            if (App.CurrentSession == null || string.IsNullOrWhiteSpace(App.CurrentSession.Token))
            {
                _dialogs.Warning(Resource("NoSessionMessage"), Resource("NotLoggedInTitle"));
                App.Logout();
                return;
            }
            IsBusy = true;
            try
            {
                SetStatus(Resource("FetchingRemarks"), false);
                WorkOrderRemarkResponse response = await _network.GetWorkOrderRemarksAsync(App.CurrentSession.Token);
                _search.Replace(response.WorkOrders);
                WebStatus = Resource("WebNormal");
                WebLastTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                SetStatus(FormatResource("FetchSuccess", _search.Count, response.UrgentCount), false);
                if (showResult)
                {
                    _dialogs.Information(FormatResource("FetchDialogMessage", response.ReturnedCount, response.UrgentCount, response.HasMore ? Resource("Yes") : Resource("No")), Resource("WorkOrderRemarksTitle"));
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                WebStatus = Resource("WebSessionExpired");
                _dialogs.Warning(ex.Message, Resource("LoginExpiredTitle"));
                App.Logout();
            }
            catch (Exception ex)
            {
                WebStatus = Resource("WebError");
                SetStatus(FormatResource("FetchFailed", ex.Message), true);
                if (showResult)
                {
                    _dialogs.Error(ex.Message, Resource("FetchFailedTitle"));
                }
            }
            finally
            {
                IsBusy = false;
                RequestFocus();
            }
        }

        private void ImportUrgentWorkOrders()
        {
            string filePath = _dialogs.SelectUrgentWorkOrderFile();
            if (string.IsNullOrWhiteSpace(filePath))
            {
                RequestFocus();
                return;
            }
            try
            {
                UrgentWorkOrderImportResult imported = _workOrderImport.Import(filePath);
                _search.ReplaceImported(imported.Rules);
                SetStatus(FormatResource("UrgentImportSuccess", imported.ImportedRowCount, imported.SnRuleCount, imported.OidRuleCount), false);
                _dialogs.Information(FormatResource("UrgentImportDialog", imported.ImportedRowCount, imported.SnRuleCount, imported.OidRuleCount), Resource("ImportCompleteTitle"));
            }
            catch (Exception ex)
            {
                SetStatus(FormatResource("UrgentImportFailed", ex.Message), true);
                _dialogs.Error(ex.Message, Resource("UrgentImportFailedTitle"));
            }
            finally
            {
                RequestFocus();
            }
        }

        private void OpenLog()
        {
            try
            {
                _scanning.OpenLog();
            }
            catch (Exception ex)
            {
                SetStatus(FormatResource("OpenLogFailed", ex.Message), true);
            }
            finally
            {
                RequestFocus();
            }
        }

        private async Task UploadLogAsync()
        {
            IsBusy = true;
            try
            {
                SetStatus(Resource("UploadingScanLog"), false);
                ScanUploadResult result = await _upload.UploadAsync(_scanning.LogFilePath);
                SetStatus(FormatResource("UploadScanLogSuccess", result.FileName), false);
                _dialogs.Information(FormatResource("UploadScanLogDialog", result.FileName, FormatFileSize(result.Size)), Resource("UploadCompleteTitle"));
            }
            catch (Exception ex)
            {
                _scanning.WriteError(ex);
                SetStatus(FormatResource("UploadScanLogFailed", ex.Message), true);
                _dialogs.Error(ex.Message, Resource("UploadFailedTitle"));
            }
            finally
            {
                IsBusy = false;
                RequestFocus();
            }
        }

        private void ChangeLanguage(string languageCode)
        {
            UiText.ChangeLanguage(languageCode);
            RefreshLocalizedText();
            RequestFocus();
        }

        private void SetStatus(string text, bool error)
        {
            StatusText = text;
            StatusBrush = error ? Brushes.DarkRed : Brushes.DarkGreen;
        }

        private static string Resource(string key)
        {
            object value = Application.Current.TryFindResource(key);
            return value == null ? key : value.ToString();
        }

        private static string FormatResource(string key, params object[] values)
        {
            return string.Format(Resource(key), values);
        }

        private static void TryCopy(string text)
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch
            {
            }
        }

        private void RequestFocus()
        {
            FocusRequested?.Invoke(this, EventArgs.Empty);
        }

        private static int NormalizePrintCopies(int copies)
        {
            return copies >= 0 && copies <= 2 ? copies : 2;
        }

        private static string FormatFileSize(long bytes)
        {
            return bytes >= 1024 * 1024
                ? (bytes / 1024d / 1024d).ToString("0.##") + " MB"
                : (bytes / 1024d).ToString("0.##") + " KB";
        }
    }
}
