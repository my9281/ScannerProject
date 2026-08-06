using Scanner.Helpers;
using Scanner.Models;
using Scanner.Services;
using System;
using System.Collections.Generic;
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
        private readonly WorkOrderSearchService _search;
        private readonly CsvImportHelper _csvImport;
        private readonly DialogHelper _dialogs;
        private string _scanCode;
        private string _statusText;
        private Brush _statusBrush = Brushes.DarkGreen;
        private string _printerText;
        private string _webStatus;
        private string _webLastTime;
        private bool _isPrint = true;
        private bool _isBusy;
        public MainWindowViewModel() : this(new NetworkHelper(), new PrintingHelper(), new ScanService(), new WorkOrderSearchService(), new CsvImportHelper(), new DialogHelper())
        {
        }

        internal MainWindowViewModel(NetworkHelper network, PrintingHelper printing, ScanService scanning, WorkOrderSearchService search, CsvImportHelper csvImport, DialogHelper dialogs)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _printing = printing ?? throw new ArgumentNullException(nameof(printing));
            _scanning = scanning ?? throw new ArgumentNullException(nameof(scanning));
            _search = search ?? throw new ArgumentNullException(nameof(search));
            _csvImport = csvImport ?? throw new ArgumentNullException(nameof(csvImport));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            ProcessScanCommand = new RelayCommand(ProcessScan, () => !IsBusy);
            OpenLogCommand = new RelayCommand(OpenLog, () => !IsBusy);
            RefreshCommand = new RelayCommand(async () => await RefreshWorkOrdersAsync(true), () => !IsBusy);
            ImportCsvCommand = new RelayCommand(ImportCsv, () => !IsBusy);
            ChangeLanguageCommand = new RelayCommand(parameter => ChangeLanguage(parameter as string));
            RefreshLocalizedText();
        }

        public event EventHandler FocusRequested;
        public ICommand ProcessScanCommand { get; }
        public ICommand OpenLogCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ImportCsvCommand { get; }
        public ICommand ChangeLanguageCommand { get; }
        public string ScanCode { get => _scanCode; set => SetProperty(ref _scanCode, value); }
        public bool IsPrint { get => _isPrint; set => SetProperty(ref _isPrint, value); }

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
        public string OperatorText => App.CurrentSession == null ? string.Empty : App.CurrentSession.Operator + " / " + App.CurrentSession.Role;
        public string LogFileText => FormatResource("LogFileValue", _scanning.LogFilePath);
        public string CountText => FormatResource("ScanCount", _scanning.ScanCount);
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
            RaisePropertyChanged(nameof(CountText));
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
                WorkOrderRemark matched = _search.Find(code);
                string printCode = matched == null || string.IsNullOrWhiteSpace(matched.Sn) ? code : matched.Sn.Trim();
                if (scan.Oid.IsOid && !scan.Oid.ShouldPrint)
                {
                    SetStatus("识别为 OID，已记录，本次不打印。", false);
                }
                else if (scan.Oid.ShouldPrint)
                {
                    _printing.PrintLabel(printCode, 1, matched);
                    SetStatus("OID 重复扫描，已打印标签：" + printCode, false);
                }
                else if (IsPrint)
                {
                    _printing.PrintLabel(printCode, 2, matched);
                    SetStatus(matched != null && matched.IsUrgent ? "已打印紧急工单标签：" + printCode : FormatResource("SavedAndPrinted", printCode), false);
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
            if (App.CurrentSession == null || string.IsNullOrWhiteSpace(App.CurrentSession.Token))
            {
                _dialogs.Warning("当前没有有效登录信息，请重新登录。", "未登录");
                App.Logout();
                return;
            }
            IsBusy = true;
            try
            {
                SetStatus("正在获取工单备注……", false);
                WorkOrderRemarkResponse response = await _network.GetWorkOrderRemarksAsync(App.CurrentSession.Token);
                _search.Replace(response.WorkOrders);
                WebStatus = "正常";
                WebLastTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                SetStatus($"工单备注获取成功，共返回 {_search.Count} 条，紧急工单 {response.UrgentCount} 条。", false);
                if (showResult)
                {
                    _dialogs.Information($"接口调用成功。\n\n返回工单：{response.ReturnedCount}\n" + $"紧急工单：{response.UrgentCount}\n" + $"是否还有更多：{(response.HasMore ? "是" : "否")}", "工单备注");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                WebStatus = "登录失效";
                _dialogs.Warning(ex.Message, "登录已失效");
                App.Logout();
            }
            catch (Exception ex)
            {
                WebStatus = "异常";
                SetStatus("获取工单备注失败：" + ex.Message, true);
                if (showResult)
                {
                    _dialogs.Error(ex.Message, "获取失败");
                }
            }
            finally
            {
                IsBusy = false;
                RequestFocus();
            }
        }

        private void ImportCsv()
        {
            string filePath = _dialogs.SelectCsvFile();
            if (string.IsNullOrWhiteSpace(filePath))
            {
                RequestFocus();
                return;
            }
            try
            {
                IReadOnlyList<WorkOrderRemark> imported = _csvImport.ImportUrgentWorkOrders(filePath);
                _search.MergeImported(imported);
                SetStatus($"CSV 导入完成，共导入 {imported.Count} 条紧急工单。", false);
                _dialogs.Information($"成功导入 {imported.Count} 条紧急工单。", "导入完成");
            }
            catch (Exception ex)
            {
                SetStatus("CSV 导入失败：" + ex.Message, true);
                _dialogs.Error(ex.Message, "CSV 导入失败");
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
                SetStatus("无法打开扫描记录：" + ex.Message, true);
            }
            finally
            {
                RequestFocus();
            }
        }

        private void ChangeLanguage(string languageCode)
        {
            string path;
            switch (languageCode)
            {
                case "en-US":
                    path = "Languages/Language.en-US.xaml";
                    break;
                case "es-ES":
                    path = "Languages/Language.es-ES.xaml";
                    break;
                default:
                    path = "Languages/Language.zh-CN.xaml";
                    break;
            }
            var dictionary = new ResourceDictionary
            {
                Source = new Uri(path, UriKind.Relative)
            };
            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(dictionary);
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
    }
}
