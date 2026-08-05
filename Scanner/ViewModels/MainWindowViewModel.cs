using Scanner.Helpers;
using Scanner.Models;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly ScanLogHelper _scanLog;
        private readonly CsvImportHelper _csvImport;
        private readonly DialogHelper _dialogs;
        private List<WorkOrderRemark> _workOrders = new List<WorkOrderRemark>();
        private string _scanCode;
        private string _statusText;
        private Brush _statusBrush = Brushes.DarkGreen;
        private string _printerText;
        private string _webStatus;
        private string _webLastTime;
        private int _scanCount;
        private bool _isPrint = true;
        private bool _isBusy;

        public MainWindowViewModel()
            : this(new NetworkHelper(), new PrintingHelper(), new ScanLogHelper(), new CsvImportHelper(), new DialogHelper())
        {
        }

        internal MainWindowViewModel(NetworkHelper network, PrintingHelper printing, ScanLogHelper scanLog, CsvImportHelper csvImport, DialogHelper dialogs)
        {
            _network = network;
            _printing = printing;
            _scanLog = scanLog;
            _csvImport = csvImport;
            _dialogs = dialogs;
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

        public string ScanCode
        {
            get => _scanCode;
            set => SetProperty(ref _scanCode, value);
        }

        public bool IsPrint
        {
            get => _isPrint;
            set => SetProperty(ref _isPrint, value);
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
        public string OperatorText => App.CurrentSession == null ? string.Empty : App.CurrentSession.Operator + " / " + App.CurrentSession.Role;
        public string LogFileText => FormatResource("LogFileValue", _scanLog.FilePath);
        public string CountText => FormatResource("ScanCount", _scanCount);
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
                _scanLog.Append(code);
                _scanCount++;
                RaisePropertyChanged(nameof(CountText));
                WorkOrderRemark matched = FindWorkOrder(code);
                string printCode = matched == null || string.IsNullOrWhiteSpace(matched.Sn) ? code : matched.Sn.Trim();
                if (IsPrint)
                {
                    _printing.PrintLabel(printCode, 1, matched);
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
                _scanLog.WriteError(ex);
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
                _workOrders = response.WorkOrders ?? new List<WorkOrderRemark>();
                WebStatus = "正常";
                WebLastTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                SetStatus($"工单备注获取成功，共返回 {_workOrders.Count} 条，紧急工单 {response.UrgentCount} 条。", false);
                if (showResult)
                {
                    _dialogs.Information($"接口调用成功。\n\n返回工单：{response.ReturnedCount}\n紧急工单：{response.UrgentCount}\n是否还有更多：{(response.HasMore ? "是" : "否")}", "工单备注");
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
                foreach (WorkOrderRemark item in imported.Reverse())
                {
                    _workOrders.RemoveAll(existing => SameIdentity(existing, item));
                    _workOrders.Insert(0, item);
                }

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
                _scanLog.Open();
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
                case "en-US": path = "Languages/Language.en-US.xaml"; break;
                case "es-ES": path = "Languages/Language.es-ES.xaml"; break;
                default: path = "Languages/Language.zh-CN.xaml"; break;
            }

            var dictionary = new ResourceDictionary { Source = new Uri(path, UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(dictionary);
            RefreshLocalizedText();
            RequestFocus();
        }

        private WorkOrderRemark FindWorkOrder(string code)
        {
            return _workOrders.FirstOrDefault(item => item != null &&
                (EqualsCode(item.Sn, code) || EqualsCode(item.TrackingNumber, code)));
        }

        private static bool SameIdentity(WorkOrderRemark left, WorkOrderRemark right)
        {
            return left != null && right != null &&
                ((!string.IsNullOrWhiteSpace(right.Sn) && EqualsCode(left.Sn, right.Sn)) ||
                 (!string.IsNullOrWhiteSpace(right.TrackingNumber) && EqualsCode(left.TrackingNumber, right.TrackingNumber)));
        }

        private static bool EqualsCode(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
                   string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
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
            try { Clipboard.SetText(text); } catch { }
        }

        private void RequestFocus()
        {
            FocusRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
