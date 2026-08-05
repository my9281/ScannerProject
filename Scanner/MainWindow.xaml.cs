using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using Scanner.Models;
using Scanner.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Printing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
// 使用别名，避免 System.Drawing 与 WPF 类型重名
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingImageFormat = System.Drawing.Imaging.ImageFormat;
using GdiMargins = System.Drawing.Printing.Margins;
using GdiPaperSize = System.Drawing.Printing.PaperSize;
using GdiPrintDocument = System.Drawing.Printing.PrintDocument;
using GdiPrintPageEventArgs = System.Drawing.Printing.PrintPageEventArgs;
using GdiRectangle = System.Drawing.Rectangle;
using GdiStandardPrintController = System.Drawing.Printing.StandardPrintController;

namespace Scanner
{
    public partial class MainWindow : Window
    {
        /*
         * CSV 数组索引从 0 开始。
         *
         * Excel J  列 = 第 10 列 = 索引 9
         * Excel Y  列 = 第 25 列 = 索引 24
         * Excel AD 列 = 第 30 列 = 索引 29
         * Excel AR 列 = 第 44 列 = 索引 43
         */
        private const int CsvUrgentColumnIndex = 9;
        private const int CsvSnColumnIndex = 24;
        private const int CsvTrackingColumnIndex = 29;
        private const int CsvRemarkColumnIndex = 43;
        private const double LabelWidth = 576.0;
        private const double LabelHeight = 384.0;
        private bool? m_IsPrint = true;
        private const int PrintCopies = 1;
        private string _currentLanguage = "zh-CN";
        private readonly string _logFilePath;

        private int _scanCount;
        private bool _isPrinting;

        public bool? IsPrint
        {
            get => m_IsPrint;
            set
            {
                m_IsPrint = value;
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            Activated += MainWindow_Activated;
            /*
             * 保存到用户的“文档”目录。
             *
             * 这样即使程序安装在 Program Files，
             * 也不会因为没有写入权限而失败。
             */
            string documentsFolder =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments
                );

            string logFolder =
                Path.Combine(
                    documentsFolder,
                    "SN Label Printer"
                );

            Directory.CreateDirectory(logFolder);

            _logFilePath =
                Path.Combine(
                    logFolder,
                    "scanned_codes.txt"
                );

            _scanCount = 0;
            _isPrinting = false;
        }


        private void MainWindow_Activated(object sender, System.EventArgs e)
        {
            SetEnglishInput();
        }
        private void SetEnglishInput()
        {
            try
            {
                CultureInfo englishCulture = CultureInfo.GetCultureInfo("en-US");

                InputLanguageManager.Current.CurrentInputLanguage =
                    englishCulture;

                InputMethod.SetPreferredImeState(
                    SnTextBox,
                    InputMethodState.Off);

                InputMethod.SetIsInputMethodEnabled(
                    SnTextBox,
                    false);

                SnTextBox.Focus();
                Keyboard.Focus(SnTextBox);
            }
            catch
            {
                // 不影响程序继续运行
            }
        }
        private void LanguageButton_Click(
    object sender,
    RoutedEventArgs e)
        {
            Button button = sender as Button;

            if (button == null)
            {
                return;
            }

            string languageCode =
                button.Tag as string;

            if (string.IsNullOrWhiteSpace(languageCode))
            {
                return;
            }

            ChangeLanguage(languageCode);

            FocusScannerInput();
        }
        private void ChangeLanguage(string languageCode)
        {
            string resourcePath;

            switch (languageCode)
            {
                case "en-US":
                    resourcePath =
                        "Languages/Language.en-US.xaml";
                    break;

                case "es-ES":
                    resourcePath =
                        "Languages/Language.es-ES.xaml";
                    break;

                default:
                    languageCode = "zh-CN";
                    resourcePath =
                        "Languages/Language.zh-CN.xaml";
                    break;
            }

            ResourceDictionary languageDictionary =
                new ResourceDictionary();

            languageDictionary.Source =
                new Uri(
                    resourcePath,
                    UriKind.Relative
                );

            var dictionaries =
                Application.Current.Resources
                    .MergedDictionaries;

            dictionaries.Clear();
            dictionaries.Add(languageDictionary);

            _currentLanguage = languageCode;

            RefreshRuntimeText();
        }
        private string GetText(
    string resourceKey,
    params object[] arguments)
        {
            string format =
                GetText(resourceKey);

            return string.Format(
                format,
                arguments
            );
        }

        private void RefreshRuntimeText()
        {
            try
            {
                PrintQueue printer =
                    GetDefaultPrintQueue();

                PrinterTextBlock.Text =
                    GetText(
                        "DefaultPrinter",
                        printer.FullName
                    );
            }
            catch
            {
                PrinterTextBlock.Text =
                    GetText("PrinterNotFound");
            }

            LogFileTextBlock.Text =
                GetText(
                    "LogFileValue",
                    _logFilePath
                );

            CountTextBlock.Text =
                GetText(
                    "ScanCount",
                    _scanCount
                );

            /*
             * 切换语言时统一恢复成等待扫描。
             */
            StatusTextBlock.Text =
                GetText("WaitingForScan");

            StatusTextBlock.Foreground =
                Brushes.DarkGreen;
        }

        private string GetText(
    string resourceKey)
        {
            object value =
                Application.Current.TryFindResource(
                    resourceKey
                );

            if (value == null)
            {
                return resourceKey;
            }

            return value.ToString();
        }

        private async void Window_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            RefreshRuntimeText();

            if (App.CurrentSession != null)
            {
                OperatorTextBlock.Text =
                    App.CurrentSession.Operator
                    + " / "
                    + App.CurrentSession.Role;
            }

            SetEnglishInput();

            bool flowControl =
                await GetAlertList();

            if (!flowControl)
            {
                FocusScannerInput();
                return;
            }

            FocusScannerInput();
        }
        private void Window_PreviewMouseDown(
            object sender,
            MouseButtonEventArgs e)
        {
            /*
             * 点击窗口其他空白位置后，
             * 再把焦点返回扫码输入框。
             *
             * 点击按钮时不强制抢走焦点，
             * 避免影响按钮操作。
             */
            DependencyObject source =
                e.OriginalSource as DependencyObject;

            if (FindParent<Button>(source) == null && FindParent<CheckBox>(source) == null)
            {
                Dispatcher.BeginInvoke(
                    new Action(FocusScannerInput)
                );
            }
        }

        private void SnTextBox_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            e.Handled = true;

            ProcessScan();
        }

        private void PrintButton_Click(
            object sender,
            RoutedEventArgs e)
        { 
        }

        private void OpenLogButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                EnsureLogFileExists();

                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = _logFilePath,
                        UseShellExecute = true
                    }
                );
            }
            catch (Exception ex)
            {
                SetStatus(
                    "无法打开扫描记录：" + ex.Message,
                    true
                );
            }
            finally
            {
                FocusScannerInput();
            }
        }
        private void ProcessScan()
        {
            if (_isPrinting)
            {
                SetStatus(GetText("BusyProcessing"), true);
                return;
            }

            string scannedCode = SnTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(scannedCode))
            {
                SetStatus(GetText("EmptyCode"), true);
                SnTextBox.Clear();
                FocusScannerInput();
                return;
            }

            _isPrinting = true;
            PrintButton.IsEnabled = false;
            SnTextBox.IsEnabled = false;

            try
            {
                try
                {
                    Clipboard.SetText(scannedCode);
                }
                catch
                {
                    // 剪贴板暂时被占用时，不影响记录和打印。
                }

                SetStatus(GetText("Processing", scannedCode), false);
                SaveScannedCode(scannedCode);
                _scanCount++;
                UpdateScanCount();

                WorkOrderRemark matchedWorkOrder =
                    FindMatchedWorkOrder(scannedCode);

                string printSn = scannedCode;

                // 扫描物流编号时，标签打印工单中的真实 SN。
                if (matchedWorkOrder != null &&
                    !string.IsNullOrWhiteSpace(matchedWorkOrder.Sn))
                {
                    printSn = matchedWorkOrder.Sn.Trim();
                }

                if (IsPrint == true)
                {
                    PrintLabelCopies(
                        printSn,
                        PrintCopies,
                        matchedWorkOrder
                    );

                    if (matchedWorkOrder != null &&
                        matchedWorkOrder.IsUrgent)
                    {
                        SetStatus(
                            "已打印紧急工单标签：" + printSn,
                            false
                        );
                    }
                    else
                    {
                        SetStatus(
                            GetText("SavedAndPrinted", printSn),
                            false
                        );
                    }
                }
                else
                {
                    SetStatus(
                        GetText("SavedWithoutPrint", scannedCode),
                        false
                    );
                }

                SnTextBox.Clear();
            }
            catch (Exception ex)
            {
                WriteErrorLog(ex.ToString());
                SetStatus(GetText("ProcessFailed", ex.Message), true);
                SnTextBox.SelectAll();
            }
            finally
            {
                _isPrinting = false;
                PrintButton.IsEnabled = true;
                SnTextBox.IsEnabled = true;
                FocusScannerInput();
            }
        }
        private void ProcessScan2()
        {
            var collection = new List<string>();
            collection.Add("1050355L287242100343".Replace(" ", ""));
            collection.Add("1090051GK31254100081".Replace(" ", ""));
            collection.Add("1100159GK31254600146".Replace(" ", ""));
            collection.Add("1100159GK31254600317".Replace(" ", ""));
            collection.Add("1100159GK31254600548".Replace(" ", ""));
            collection.Add("1100159GK31254601302".Replace(" ", ""));
            collection.Add("1100159GK31254601330".Replace(" ", ""));
            collection.Add("1100159GK31254601588".Replace(" ", ""));
            collection.Add("AC1802339003060101".Replace(" ", ""));
            collection.Add("AC1802411002158064".Replace(" ", ""));
            collection.Add("AC1802435001578484".Replace(" ", ""));
            collection.Add("AC1802439000801439".Replace(" ", ""));
            collection.Add("AC1802441001023528".Replace(" ", ""));
            collection.Add("AC200L2442001344037".Replace(" ", ""));
            collection.Add("AC200M2239000749473".Replace(" ", ""));
            collection.Add("AC2A2449000726271".Replace(" ", ""));
            collection.Add("AC2P2509000962309".Replace(" ", ""));
            collection.Add("AC5002239000795314".Replace(" ", ""));
            collection.Add("AC50B2451000919757".Replace(" ", ""));
            collection.Add("AC50B2451001122193".Replace(" ", ""));
            collection.Add("AC702444001363827".Replace(" ", ""));
            collection.Add("AC702539011206091".Replace(" ", ""));
            collection.Add("AC702540003256433".Replace(" ", ""));
            collection.Add("AP3002531000129038".Replace(" ", ""));
            collection.Add("AP3002531000608388".Replace(" ", ""));
            collection.Add("EB3A2219003654099".Replace(" ", ""));
            collection.Add("EB3A2234002740368".Replace(" ", ""));
            collection.Add("EB3A2337001560978".Replace(" ", ""));
            collection.Add("EB3A2430001996722".Replace(" ", ""));
            collection.Add("EB3A2430002079298".Replace(" ", ""));
            collection.Add("EB3A2431001927554".Replace(" ", ""));
            collection.Add("EB3A2431002359808".Replace(" ", ""));
            collection.Add("EB3A2431002430292".Replace(" ", ""));
            collection.Add("EB3A2431002605416".Replace(" ", ""));
            collection.Add("EB3A2438002679530".Replace(" ", ""));
            collection.Add("EB3A2450003196815".Replace(" ", ""));
            collection.Add("EB3A2452002039347".Replace(" ", ""));
            collection.Add("EL100V22527005968837".Replace(" ", ""));
            collection.Add("EL100V22527006402107".Replace(" ", ""));
            collection.Add("EL100V22528040264876".Replace(" ", ""));
            collection.Add("EL100V22532010898666".Replace(" ", ""));
            collection.Add("EL100V22537012017994".Replace(" ", ""));
            collection.Add("EL100V22537013422276".Replace(" ", ""));
            collection.Add("EL100V22537013850741".Replace(" ", ""));
            collection.Add("EL100V22537014131925".Replace(" ", ""));
            collection.Add("EL100V22539000338660".Replace(" ", ""));
            collection.Add("EL100V22539005876918".Replace(" ", ""));
            collection.Add("EL100V22542002820747".Replace(" ", ""));
            collection.Add("EL102539215131399".Replace(" ", ""));
            collection.Add("EL102539215178604".Replace(" ", ""));
            collection.Add("EL102541211047466".Replace(" ", ""));
            collection.Add("EL102541211354166".Replace(" ", ""));
            collection.Add("EL102543131862105".Replace(" ", ""));
            collection.Add("EL102543131924698".Replace(" ", ""));
            collection.Add("EL102543132237766".Replace(" ", ""));
            collection.Add("EL102543132310042".Replace(" ", ""));
            collection.Add("EL102543133066966".Replace(" ", ""));
            collection.Add("EL102543133267512".Replace(" ", ""));
            collection.Add("EL102543133360685".Replace(" ", ""));
            collection.Add("EL102543133890986".Replace(" ", ""));
            collection.Add("EL102551137637068".Replace(" ", ""));
            collection.Add("EL3002601136729676".Replace(" ", ""));
            collection.Add("EL3002610132806670".Replace(" ", ""));
            collection.Add("EL3002613132758700".Replace(" ", ""));
            collection.Add("EL3002613133425247".Replace(" ", ""));
            collection.Add("EL3002613134493750".Replace(" ", ""));
            collection.Add("EL30V22531020918657".Replace(" ", ""));
            collection.Add("EL30V22531020980874".Replace(" ", ""));
            collection.Add("EL30V22531021130450".Replace(" ", ""));
            collection.Add("EL30V22531021143887".Replace(" ", ""));
            collection.Add("EL30V22531040750106".Replace(" ", ""));
            collection.Add("EL30V22531040828312".Replace(" ", ""));
            collection.Add("EL30V22531090333554".Replace(" ", ""));
            collection.Add("EL30V22531090398147".Replace(" ", ""));
            collection.Add("EL30V22531090456925".Replace(" ", ""));
            collection.Add("EL30V22531090630577".Replace(" ", ""));
            collection.Add("EL30V22531090639428".Replace(" ", ""));
            collection.Add("EL30V22537001015121".Replace(" ", ""));
            collection.Add("EL30V22539000501930".Replace(" ", ""));
            collection.Add("EL30V22543130255623".Replace(" ", ""));
            collection.Add("EL30V22543130301016".Replace(" ", ""));
            collection.Add("EL30V22543130631610".Replace(" ", ""));
            collection.Add("EL30V22543131001564".Replace(" ", ""));
            collection.Add("EL30V22543131496532".Replace(" ", ""));
            collection.Add("EL30V22543131504740".Replace(" ", ""));
            collection.Add("EL30V22543131846093".Replace(" ", ""));
            collection.Add("EL30V22543131952764".Replace(" ", ""));
            collection.Add("EL30V22543133170280".Replace(" ", ""));
            collection.Add("EL30V22543135509071".Replace(" ", ""));
            collection.Add("EL30V22543137174134".Replace(" ", ""));
            collection.Add("EL30V22543139200064".Replace(" ", ""));
            collection.Add("EL30V22544230343930".Replace(" ", ""));
            collection.Add("EL30V22550130173628".Replace(" ", ""));
            collection.Add("EL30V22550130594814".Replace(" ", ""));
            collection.Add("EL30V22550130742938".Replace(" ", ""));
            collection.Add("EL30V22550130807887".Replace(" ", ""));
            collection.Add("EL30V22550131067385".Replace(" ", ""));
            collection.Add("EL30V22550131072247".Replace(" ", ""));
            collection.Add("EL30V22550131623937".Replace(" ", ""));
            collection.Add("EL30V22550132025284".Replace(" ", ""));
            collection.Add("EL30V22550132085819".Replace(" ", ""));
            collection.Add("EL30V22550133972362".Replace(" ", ""));
            collection.Add("EL30V22550134014685".Replace(" ", ""));
            collection.Add("EL30V22550136050690".Replace(" ", ""));
            collection.Add("EL30V22550136807224".Replace(" ", ""));
            collection.Add("EL30V22550136831302".Replace(" ", ""));
            collection.Add("EL30V22550136866231".Replace(" ", ""));
            collection.Add("EL30V22550139101631".Replace(" ", ""));
            collection.Add("EL30V22550139273396".Replace(" ", ""));
            collection.Add("EL30V22550139328109".Replace(" ", ""));
            collection.Add("EL30V22605130785806".Replace(" ", ""));
            collection.Add("EL30V22605130824920".Replace(" ", ""));
            collection.Add("EL30V22605131317985".Replace(" ", ""));
            collection.Add("EL30V22605131576462".Replace(" ", ""));
            collection.Add("EL30V22605131825912".Replace(" ", ""));
            collection.Add("EL30V22605131902771".Replace(" ", ""));
            collection.Add("EL30V22605131908153".Replace(" ", ""));
            collection.Add("EL30V22606130058365".Replace(" ", ""));
            collection.Add("EL30V22606135874576".Replace(" ", ""));
            collection.Add("EL30V22606137007846".Replace(" ", ""));
            collection.Add("EL30V22606137009854".Replace(" ", ""));
            collection.Add("EL30V22606137074213".Replace(" ", ""));
            collection.Add("EL30V22606137180153".Replace(" ", ""));
            collection.Add("EL30V22606137266830".Replace(" ", ""));
            collection.Add("EL30V22606137268721".Replace(" ", ""));
            collection.Add("EL30V22606137455440".Replace(" ", ""));
            collection.Add("EL30V22606137506542".Replace(" ", ""));
            collection.Add("EL30V22606137509752".Replace(" ", ""));
            collection.Add("EL30V22606137602428".Replace(" ", ""));
            collection.Add("EL30V22606137789785".Replace(" ", ""));
            collection.Add("EL30V22606137984970".Replace(" ", ""));
            collection.Add("EL30V22606138036370".Replace(" ", ""));
            collection.Add("EL30V22606138219532".Replace(" ", ""));
            collection.Add("EL30V22606138370673".Replace(" ", ""));
            collection.Add("EL30V22606138995420".Replace(" ", ""));
            collection.Add("EL30V22606139271062".Replace(" ", ""));
            collection.Add("EL30V22606139492780".Replace(" ", ""));
            collection.Add("EL30V22606139496922".Replace(" ", ""));
            collection.Add("EL30V22606139646165".Replace(" ", ""));
            collection.Add("EL4002604235362146".Replace(" ", ""));

            foreach (var item in collection)
            {
                Thread.Sleep(500);
                if (_isPrinting)
                {
                    SetStatus(GetText("BusyProcessing"), true);
                    return;
                }

                string scannedCode = item.Trim();

                if (string.IsNullOrWhiteSpace(scannedCode))
                {
                    SetStatus(GetText("EmptyCode"), true);
                    SnTextBox.Clear();
                    FocusScannerInput();
                    return;
                }

                _isPrinting = true;
                PrintButton.IsEnabled = false;
                SnTextBox.IsEnabled = false;

                try
                {
                    try
                    {
                        Clipboard.SetText(scannedCode);
                    }
                    catch
                    {
                        // 剪贴板暂时被占用时，不影响记录和打印。
                    }

                    SetStatus(GetText("Processing", scannedCode), false);
                    SaveScannedCode(scannedCode);
                    _scanCount++;
                    UpdateScanCount();

                    WorkOrderRemark matchedWorkOrder =
                        FindMatchedWorkOrder(scannedCode);

                    string printSn = scannedCode;

                    // 扫描物流编号时，标签打印工单中的真实 SN。
                    if (matchedWorkOrder != null &&
                        !string.IsNullOrWhiteSpace(matchedWorkOrder.Sn))
                    {
                        printSn = matchedWorkOrder.Sn.Trim();
                    }

                    if (IsPrint == true)
                    {
                        PrintLabelCopies(
                            printSn,
                            PrintCopies,
                            matchedWorkOrder
                        );

                        if (matchedWorkOrder != null &&
                            matchedWorkOrder.IsUrgent)
                        {
                            SetStatus(
                                "已打印紧急工单标签：" + printSn,
                                false
                            );
                        }
                        else
                        {
                            SetStatus(
                                GetText("SavedAndPrinted", printSn),
                                false
                            );
                        }
                    }
                    else
                    {
                        SetStatus(
                            GetText("SavedWithoutPrint", scannedCode),
                            false
                        );
                    }

                    SnTextBox.Clear();
                }
                catch (Exception ex)
                {
                    WriteErrorLog(ex.ToString());
                    SetStatus(GetText("ProcessFailed", ex.Message), true);
                    SnTextBox.SelectAll();
                }
                finally
                {
                    _isPrinting = false;
                    PrintButton.IsEnabled = true;
                    SnTextBox.IsEnabled = true;
                    FocusScannerInput();
                }
            }

        }
        private void SaveScannedCode(string sn)
        {
            EnsureLogFileExists();
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + sn + Environment.NewLine;

            File.AppendAllText(
                _logFilePath,
                line,
                new UTF8Encoding(true)
            );
        }

        private void EnsureLogFileExists()
        {
            string folder =
                Path.GetDirectoryName(
                    _logFilePath
                );

            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            if (!File.Exists(_logFilePath))
            {
                File.WriteAllText(
                    _logFilePath,
                    string.Empty,
                    new UTF8Encoding(true)
                );
            }
        }
        private void PrintLabelCopies(
            string sn,
            int copies,
            WorkOrderRemark matchedWorkOrder)
        {
            if (copies <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    "copies",
                    "打印份数必须大于零。"
                );
            }

            Canvas labelCanvas =
                CreateLabelCanvas(
                    sn,
                    matchedWorkOrder
                );

            BitmapSource bitmapSource =
                RenderLabelToBitmap(
                    labelCanvas
                );

            using (
                DrawingBitmap bitmap =
                    ConvertBitmapSourceToDrawingBitmap(
                        bitmapSource
                    )
            )
            {
                int copyNumber;

                for (copyNumber = 1;
                     copyNumber <= copies;
                     copyNumber++)
                {
                    PrintBitmapWithGdi(
                        bitmap
                    );
                }
            }
        }

        private DrawingBitmap
            ConvertBitmapSourceToDrawingBitmap(
                BitmapSource bitmapSource)
        {
            if (bitmapSource == null)
            {
                throw new ArgumentNullException(
                    "bitmapSource"
                );
            }

            using (
                MemoryStream stream =
                    new MemoryStream()
            )
            {
                PngBitmapEncoder encoder =
                    new PngBitmapEncoder();

                encoder.Frames.Add(
                    BitmapFrame.Create(
                        bitmapSource
                    )
                );

                encoder.Save(stream);
                stream.Position = 0;

                using (
                    DrawingBitmap temporaryBitmap =
                        new DrawingBitmap(stream)
                )
                {
                    return new DrawingBitmap(
                        temporaryBitmap
                    );
                }
            }
        }

        private void PrintBitmapWithGdi(
            DrawingBitmap bitmap)
        {
            if (bitmap == null)
            {
                throw new ArgumentNullException(
                    "bitmap"
                );
            }

            using (
                GdiPrintDocument printDocument =
                    new GdiPrintDocument()
            )
            {
                printDocument.PrintController =
                    new GdiStandardPrintController();

                if (!printDocument.PrinterSettings.IsValid)
                {
                    throw new InvalidOperationException(
                        "Windows 默认打印机无效或不可用。"
                    );
                }

                printDocument.DefaultPageSettings.PaperSize =
                    new GdiPaperSize(
                        "4x6",
                        400,
                        600
                    );

                printDocument.DefaultPageSettings.Landscape =
                    true;

                printDocument.DefaultPageSettings.Margins =
                    new GdiMargins(
                        0,
                        0,
                        0,
                        0
                    );

                printDocument.OriginAtMargins =
                    false;

                printDocument.PrintPage +=
                    delegate (
                        object sender,
                        GdiPrintPageEventArgs e)
                    {
                        if (e.Graphics == null)
                        {
                            throw new InvalidOperationException(
                                "无法创建打印绘图环境。"
                            );
                        }

                        GdiRectangle targetRectangle =
                            new GdiRectangle(
                                e.PageBounds.Left,
                                e.PageBounds.Top,
                                e.PageBounds.Width,
                                e.PageBounds.Height
                            );

                        e.Graphics.DrawImage(
                            bitmap,
                            targetRectangle
                        );

                        e.HasMorePages = false;
                    };

                try
                {
                    printDocument.Print();
                }
                catch (Exception ex)
                {
                    WriteErrorLog(
                        "GDI 打印失败"
                        + Environment.NewLine
                        + "打印机："
                        + printDocument.PrinterSettings.PrinterName
                        + Environment.NewLine
                        + ex.ToString()
                    );

                    throw;
                }
            }
        }

        private PrintQueue GetDefaultPrintQueue()
        {
            PrintQueue printQueue = null;

            using (
                LocalPrintServer printServer =
                    new LocalPrintServer()
            )
            {
                printQueue =
                    printServer.DefaultPrintQueue;
            }

            if (printQueue == null)
            {
                throw new InvalidOperationException(
                    "没有找到 Windows 默认打印机。"
                );
            }

            return printQueue;
        }

        private PrintTicket CreatePrintTicket(
            PrintQueue printQueue)
        {
            PrintTicket ticket;

            if (printQueue.DefaultPrintTicket != null)
            {
                ticket =
                    printQueue.DefaultPrintTicket.Clone();
            }
            else
            {
                ticket = new PrintTicket();
            }

            /*
             * 标签页面本身已是 6 × 4 横向。
             */
            ticket.PageMediaSize =
                new PageMediaSize(
                    LabelWidth,
                    LabelHeight
                );

            ticket.PageOrientation =
                PageOrientation.Landscape;

            /*
             * 每次提交一份。
             * 外层循环会提交两次。
             */
            ticket.CopyCount = 1;

            return ticket;
        }
        private Canvas CreateLabelCanvas(
            string sn,
            WorkOrderRemark matchedWorkOrder)
        {
            sn = SanitizeForXps(sn);

            string lastFive = GetLastFive(sn);
            string currentDate = DateTime.Now.ToString("yyyy-MM-dd");

            bool isUrgent =
                matchedWorkOrder != null &&
                matchedWorkOrder.IsUrgent;

            string shortRemark = string.Empty;

            if (isUrgent)
            {
                shortRemark = GetShortRemark(
                    matchedWorkOrder.Remark,
                    50
                );
            }

            BitmapSource qrCodeImage =
                CreateQrCodeImage(sn, 190, 190);

            BitmapSource barcodeImage =
                CreateBarcodeImage(sn, 430, 85);

            Canvas labelCanvas = new Canvas();
            labelCanvas.Width = LabelWidth;
            labelCanvas.Height = LabelHeight;
            labelCanvas.Background = Brushes.White;

            TextBlock snText =
                CreateLabelText(
                    "SN : " + sn,
                    27.0,
                    35.0,
                    12.0
                );

            snText.Width = LabelWidth - 70.0;

            TextBlock lastFiveText =
                CreateLabelText(
                    lastFive,
                    52.0,
                    30.0,
                    62.0
                );

            lastFiveText.Width = 280.0;

            System.Windows.Controls.Image qrImage =
                new System.Windows.Controls.Image();

            qrImage.Source = qrCodeImage;
            qrImage.Width = 105.0;
            qrImage.Height = 105.0;
            qrImage.Stretch = Stretch.Uniform;
            Canvas.SetLeft(qrImage, LabelWidth - 135.0);
            Canvas.SetTop(qrImage, 52.0);

            if (isUrgent)
            {
                Border urgentBorder = new Border();
                urgentBorder.Width = 150.0;
                urgentBorder.Height = 46.0;
                urgentBorder.BorderBrush = Brushes.Black;
                urgentBorder.BorderThickness = new Thickness(3.0);
                urgentBorder.Background = Brushes.White;

                TextBlock urgentText = new TextBlock();
                urgentText.Text = "紧急";
                urgentText.FontSize = 32.0;
                urgentText.FontWeight = FontWeights.Bold;
                urgentText.FontFamily =
                    new FontFamily("Microsoft YaHei UI");
                urgentText.Foreground = Brushes.Black;
                urgentText.TextAlignment = TextAlignment.Center;
                urgentText.VerticalAlignment = VerticalAlignment.Center;

                urgentBorder.Child = urgentText;
                Canvas.SetLeft(urgentBorder, 95.0);
                Canvas.SetTop(urgentBorder, 118.0);
                labelCanvas.Children.Add(urgentBorder);
            }

            System.Windows.Controls.Image barcode =
                new System.Windows.Controls.Image();

            barcode.Source = barcodeImage;
            barcode.Width = 420.0;
            barcode.Height = 68.0;
            barcode.Stretch = Stretch.Fill;
            Canvas.SetLeft(
                barcode,
                (LabelWidth - barcode.Width) / 2.0
            );
            Canvas.SetTop(barcode, 170.0);

            TextBlock barcodeText =
                CreateLabelText(
                    sn,
                    17.0,
                    35.0,
                    238.0
                );

            barcodeText.Width = LabelWidth - 70.0;

            if (isUrgent &&
                !string.IsNullOrWhiteSpace(shortRemark))
            {
                TextBlock remarkText = new TextBlock();
                remarkText.Text = "备注：" + shortRemark;
                remarkText.Width = LabelWidth - 60.0;
                remarkText.Height = 58.0;
                remarkText.FontSize = 18.0;
                remarkText.FontWeight = FontWeights.Bold;
                remarkText.FontFamily =
                    new FontFamily("Microsoft YaHei UI");
                remarkText.Foreground = Brushes.Black;
                remarkText.TextAlignment = TextAlignment.Left;
                remarkText.TextWrapping = TextWrapping.Wrap;
                Canvas.SetLeft(remarkText, 30.0);
                Canvas.SetTop(remarkText, 267.0);
                labelCanvas.Children.Add(remarkText);
            }

            TextBlock dateText =
                CreateLabelText(
                    "Date : " + currentDate,
                    isUrgent ? 20.0 : 25.0,
                    35.0,
                    isUrgent ? 348.0 : 310.0
                );

            dateText.Width = LabelWidth - 70.0;

            labelCanvas.Children.Add(snText);
            labelCanvas.Children.Add(lastFiveText);
            labelCanvas.Children.Add(qrImage);
            labelCanvas.Children.Add(barcode);
            labelCanvas.Children.Add(barcodeText);
            labelCanvas.Children.Add(dateText);

            return labelCanvas;
        }

        private BitmapSource RenderLabelToBitmap(
            Canvas labelCanvas)
        {
            if (labelCanvas == null)
            {
                throw new ArgumentNullException(
                    "labelCanvas"
                );
            }

            /*
             * 标签的 WPF 尺寸是 576 × 384 DIP，
             * 即 6 × 4 英寸（WPF 每英寸 96 DIP）。
             *
             * RenderTargetBitmap 设置为 300 DPI 后，
             * WPF 会自动把 576 × 384 DIP 转换成
             * 1800 × 1200 像素。
             *
             * 这里不能再手动 ScaleTransform，
             * 否则会重复放大 300/96 倍，只渲染出左上角。
             */
            const int pixelWidth = 1800;
            const int pixelHeight = 1200;
            const double dpi = 300.0;

            labelCanvas.Measure(
                new System.Windows.Size(
                    LabelWidth,
                    LabelHeight
                )
            );

            labelCanvas.Arrange(
                new Rect(
                    0,
                    0,
                    LabelWidth,
                    LabelHeight
                )
            );

            labelCanvas.UpdateLayout();

            RenderTargetBitmap bitmap =
                new RenderTargetBitmap(
                    pixelWidth,
                    pixelHeight,
                    dpi,
                    dpi,
                    PixelFormats.Pbgra32
                );

            bitmap.Render(
                labelCanvas
            );

            bitmap.Freeze();

            return bitmap;
        }

        private FixedDocument CreateBitmapLabelDocument(
            string sn,
            WorkOrderRemark matchedWorkOrder)
        {
            Canvas labelCanvas =
                CreateLabelCanvas(
                    sn,
                    matchedWorkOrder
                );

            BitmapSource labelBitmap =
                RenderLabelToBitmap(labelCanvas);

            FixedDocument document =
                new FixedDocument();

            document.DocumentPaginator.PageSize =
                new System.Windows.Size(
                    LabelWidth,
                    LabelHeight
                );

            FixedPage page = new FixedPage();
            page.Width = LabelWidth;
            page.Height = LabelHeight;
            page.Background = Brushes.White;

            System.Windows.Controls.Image image =
                new System.Windows.Controls.Image();

            image.Source = labelBitmap;
            image.Width = LabelWidth;
            image.Height = LabelHeight;
            image.Stretch = Stretch.Fill;

            FixedPage.SetLeft(image, 0.0);
            FixedPage.SetTop(image, 0.0);

            page.Children.Add(image);

            PageContent pageContent =
                new PageContent();

            IAddChild addChild =
                (IAddChild)pageContent;

            addChild.AddChild(page);
            document.Pages.Add(pageContent);

            return document;
        }

        private static Brush GetWhite()
        {
            return Brushes.White;
        }

        private BitmapSource CreateQrCodeImage(
    string content,
    int width,
    int height)
        {
            BarcodeWriter writer =
                new BarcodeWriter();

            writer.Format =
                BarcodeFormat.QR_CODE;

            writer.Options =
                new QrCodeEncodingOptions
                {
                    Width = width,
                    Height = height,
                    Margin = 1,
                    CharacterSet = "UTF-8"
                };

            using (DrawingBitmap bitmap =
                writer.Write(content))
            {
                return ConvertBitmapToBitmapSource(
                    bitmap
                );
            }
        }
        private BitmapSource CreateBarcodeImage(
    string content,
    int width,
    int height)
        {
            BarcodeWriter writer =
                new BarcodeWriter();

            writer.Format =
                BarcodeFormat.CODE_128;

            writer.Options =
                new EncodingOptions
                {
                    Width = width,
                    Height = height,
                    Margin = 2,
                    PureBarcode = true
                };

            using (DrawingBitmap bitmap =
                writer.Write(content))
            {
                return ConvertBitmapToBitmapSource(
                    bitmap
                );
            }
        }
        private BitmapSource ConvertBitmapToBitmapSource(
    DrawingBitmap bitmap)
        {
            using (MemoryStream stream =
                new MemoryStream())
            {
                bitmap.Save(
                    stream,
                    DrawingImageFormat.Png
                );

                stream.Position = 0;

                BitmapImage bitmapImage =
                    new BitmapImage();

                bitmapImage.BeginInit();
                bitmapImage.CacheOption =
                    BitmapCacheOption.OnLoad;

                bitmapImage.StreamSource =
                    stream;

                bitmapImage.EndInit();
                bitmapImage.Freeze();

                return bitmapImage;
            }
        }
        private TextBlock CreateLabelText(
            string text,
            double fontSize,
            double left,
            double top)
        {
            TextBlock textBlock =
                new TextBlock();

            textBlock.Text = text;
            textBlock.Width =
                LabelWidth - (left * 2.0);

            textBlock.FontSize = fontSize;
            textBlock.FontWeight =
                FontWeights.Bold;

            textBlock.FontFamily =
                new System.Windows.Media.FontFamily("Microsoft YaHei UI");

            textBlock.Foreground =
                Brushes.Black;

            textBlock.TextAlignment =
                TextAlignment.Center;

            textBlock.TextWrapping =
                TextWrapping.NoWrap;

            Canvas.SetLeft(
                textBlock,
                left
            );

            Canvas.SetTop(
                textBlock,
                top
            );

            return textBlock;
        }

        private string GetLastFive(
            string sn)
        {
            if (sn == null)
            {
                return string.Empty;
            }

            if (sn.Length <= 5)
            {
                return sn;
            }

            return sn.Substring(
                sn.Length - 5,
                5
            );
        }

        private void ShowPrinterInformation()
        {
            try
            {
                PrintQueue printer =
                    GetDefaultPrintQueue();

                PrinterTextBlock.Text =
                    "默认打印机："
                    + printer.FullName;
            }
            catch (Exception ex)
            {
                PrinterTextBlock.Text =
                    "默认打印机：未找到";

                SetStatus(
                    ex.Message,
                    true
                );
            }
        }

        private void UpdateScanCount()
        {
            CountTextBlock.Text =
                "本次运行已扫描："
                + _scanCount.ToString();
        }

        private void FocusScannerInput()
        {
            Dispatcher.BeginInvoke(
                new Action(
                    delegate
                    {
                        SnTextBox.Focus();

                        Keyboard.Focus(
                            SnTextBox
                        );

                        SnTextBox.CaretIndex =
                            SnTextBox.Text.Length;
                    }
                )
            );
        }

        private void SetStatus(
            string message,
            bool isError)
        {
            StatusTextBlock.Text = message;

            if (isError)
            {
                StatusTextBlock.Foreground =
                    System.Windows.Media.Brushes.DarkRed;
            }
            else
            {
                StatusTextBlock.Foreground =
                    System.Windows.Media.Brushes.DarkGreen;
            }
        }

        private static T FindParent<T>(DependencyObject child)
            where T : DependencyObject
        {
            DependencyObject current = child;

            while (current != null)
            {
                T target = current as T;

                if (target != null)
                {
                    return target;
                }

                current =
                    VisualTreeHelper.GetParent(
                        current
                    );
            }

            return null;
        }

        private string GetShortRemark(
            string remark,
            int maxLength)
        {
            string result = SanitizeForXps(remark);

            if (string.IsNullOrWhiteSpace(result))
            {
                return string.Empty;
            }

            result = result.Trim()
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("\t", " ");

            while (result.Contains("  "))
            {
                result = result.Replace("  ", " ");
            }

            if (result.Length <= maxLength)
            {
                return result;
            }

            if (maxLength <= 1)
            {
                return "…";
            }

            return result.Substring(0, maxLength - 1) + "…";
        }

        private string SanitizeForXps(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            StringBuilder builder =
                new StringBuilder(value.Length);

            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];

                if (current == '\t' ||
                    current == '\n' ||
                    current == '\r')
                {
                    builder.Append(current);
                    continue;
                }

                if (current < 0x20 ||
                    current == 0xFFFE ||
                    current == 0xFFFF)
                {
                    continue;
                }

                if (char.IsHighSurrogate(current))
                {
                    if (i + 1 < value.Length &&
                        char.IsLowSurrogate(value[i + 1]))
                    {
                        builder.Append(current);
                        builder.Append(value[i + 1]);
                        i++;
                    }

                    continue;
                }

                if (char.IsLowSurrogate(current))
                {
                    continue;
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        private void WriteErrorLog(string errorDetails)
        {
            try
            {
                string errorFile =
                    Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "error_log.txt"
                    );

                string content =
                    "========================================"
                    + Environment.NewLine
                    + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    + Environment.NewLine
                    + errorDetails
                    + Environment.NewLine;

                File.AppendAllText(
                    errorFile,
                    content,
                    Encoding.UTF8
                );
            }
            catch
            {
                // 写日志失败不影响程序继续运行。
            }
        }
        private WorkOrderRemark FindMatchedWorkOrder(string scannedCode)
        {
            if (string.IsNullOrWhiteSpace(scannedCode))
            {
                return null;
            }

            if (_workOrderRemarks == null)
            {
                return null;
            }

            string code = scannedCode.Trim();

            foreach (WorkOrderRemark item in _workOrderRemarks)
            {
                if (item == null)
                {
                    continue;
                }

                bool snMatched =
                    !string.IsNullOrWhiteSpace(item.Sn)
                    && string.Equals(
                        item.Sn.Trim(),
                        code,
                        StringComparison.OrdinalIgnoreCase
                    );

                bool trackingMatched =
                    !string.IsNullOrWhiteSpace(
                        item.TrackingNumber
                    )
                    && string.Equals(
                        item.TrackingNumber.Trim(),
                        code,
                        StringComparison.OrdinalIgnoreCase
                    );

                if (snMatched || trackingMatched)
                {
                    return item;
                }
            }

            return null;
        }
        private void ScanTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            SetEnglishInput();
        }
        private readonly WorkOrderRemarkService _workOrderRemarkService = new WorkOrderRemarkService();

        private List<WorkOrderRemark> _workOrderRemarks =
                new List<WorkOrderRemark>();
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            bool flowControl = await GetAlertList();
            if (!flowControl)
            {
                return;
            }
        }

        private async Task<bool> GetAlertList()
        {
            if (App.CurrentSession == null ||
       string.IsNullOrWhiteSpace(
           App.CurrentSession.Token
       ))
            {
                MessageBox.Show(
                    "当前没有有效登录信息，请重新登录。",
                    "未登录",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                App.Logout();

                return false;
            }

            this.IsEnabled = false;

            try
            {
                SetStatus(
                    "正在获取工单备注……",
                    false
                );

                WorkOrderRemarkResponse response =
                    await _workOrderRemarkService
                        .GetRemarksAsync(
                            App.CurrentSession.Token
                        );

                _workOrderRemarks =
                    response.WorkOrders;

                Debug.WriteLine(
                    "紧急工单数量："
                    + response.UrgentCount
                );

                Debug.WriteLine(
                    "本次返回数量："
                    + response.ReturnedCount
                );

                Debug.WriteLine(
                    "实际 List 数量："
                    + _workOrderRemarks.Count
                );

                int i;

                for (i = 0;
                     i < _workOrderRemarks.Count;
                     i++)
                {
                    WorkOrderRemark item =
                        _workOrderRemarks[i];

                    Debug.WriteLine(
                        "ID=" + item.Id
                        + " | SN=" + item.Sn
                        + " | Tracking="
                        + item.TrackingNumber
                        + " | Urgent="
                        + item.IsUrgent
                        + " | Remark="
                        + item.Remark
                    );
                }

                SetStatus(
                    "工单备注获取成功，共返回 "
                    + _workOrderRemarks.Count
                        .ToString()
                    + " 条，紧急工单 "
                    + response.UrgentCount
                        .ToString()
                    + " 条。",
                    false
                );

                MessageBox.Show(
                    "接口调用成功。\n\n"
                    + "返回工单："
                    + response.ReturnedCount
                        .ToString()
                    + "\n"
                    + "紧急工单："
                    + response.UrgentCount
                        .ToString()
                    + "\n"
                    + "是否还有更多："
                    + (
                        response.HasMore
                            ? "是"
                            : "否"
                    ),
                    "工单备注",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "登录已失效",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                App.Logout();
                return false;
            }
            catch (Exception ex)
            {
                SetStatus(
                    "获取工单备注失败："
                    + ex.Message,
                    true
                );

                MessageBox.Show(
                    ex.Message,
                    "获取失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                return false;
            }
            finally
            {
                this.IsEnabled = true;

                FocusScannerInput();
            }

            return true;
        }
        private bool ParseCsvBoolean(
    string value,
    int rowNumber)
        {
            string normalized =
                value.Trim().ToLowerInvariant();

            if (normalized == "true" ||
                normalized == "1" ||
                normalized == "yes" ||
                normalized == "y" ||
                normalized == "是" ||
                normalized == "紧急")
            {
                return true;
            }

            if (normalized == "false" ||
                normalized == "0" ||
                normalized == "no" ||
                normalized == "n" ||
                normalized == "否" ||
                normalized == "普通")
            {
                return false;
            }

            throw new InvalidOperationException(
                "CSV 第 "
                + rowNumber.ToString()
                + " 行的 is_urgent 值无效："
                + value
            );
        }
        private bool IsEmptyCsvRow(
    string[] fields)
        {
            int i;

            for (i = 0; i < fields.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(
                        fields[i]
                    ))
                {
                    return false;
                }
            }

            return true;
        }
        private int ImportUrgentCsv(
       string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException(
                    "CSV 文件路径不能为空。"
                );
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    "找不到 CSV 文件。",
                    filePath
                );
            }

            if (_workOrderRemarks == null)
            {
                _workOrderRemarks =
                    new List<WorkOrderRemark>();
            }

            int importedCount = 0;
            int skippedCount = 0;
            int rowNumber = 0;

            using (
                TextFieldParser parser =
                    new TextFieldParser(
                        filePath,
                        Encoding.UTF8
                    )
            )
            {
                parser.TextFieldType =
                    FieldType.Delimited;

                parser.SetDelimiters(",");

                parser.HasFieldsEnclosedInQuotes =
                    true;

                parser.TrimWhiteSpace =
                    false;

                while (!parser.EndOfData)
                {
                    rowNumber++;

                    string[] fields;

                    try
                    {
                        fields = parser.ReadFields();
                    }
                    catch (MalformedLineException ex)
                    {
                        throw new InvalidOperationException(
                            "CSV 第 "
                            + rowNumber.ToString()
                            + " 行格式错误："
                            + ex.Message
                        );
                    }

                    if (fields == null ||
                        IsEmptyCsvRow(fields))
                    {
                        continue;
                    }

                    /*
                     * AR 是第 44 列，因此至少需要 44 列。
                     */
                    if (fields.Length < 44)
                    {
                        skippedCount++;
                        continue;
                    }

                    /*
                     * 第一行通常是 Excel 表头。
                     * 如果 Y 列内容是 SN、序列号等，则跳过。
                     */
                    if (rowNumber == 1 &&
                        IsCsvHeaderRow(fields))
                    {
                        continue;
                    }

                    WorkOrderRemark item =
                        CreateWorkOrderFromExcelCsv(
                            fields,
                            rowNumber
                        );

                    /*
                     * J 列不包含“维修”的，不属于紧急列表。
                     */
                    if (item == null)
                    {
                        skippedCount++;
                        continue;
                    }

                    AddOrReplaceManualWorkOrder(
                        item
                    );

                    importedCount++;
                }
            }

            Debug.WriteLine(
                "CSV 导入紧急工单："
                + importedCount.ToString()
                + "，跳过："
                + skippedCount.ToString()
            );

            return importedCount;
        }
        private WorkOrderRemark
    CreateWorkOrderFromExcelCsv(
        string[] fields,
        int rowNumber)
        {
            string urgentSource =
                GetCsvFieldByIndex(
                    fields,
                    CsvUrgentColumnIndex
                );

            /*
             * J 列只要包含“维修”两个字，
             * 就作为紧急工单。
             */
            bool isUrgent =
                urgentSource.IndexOf(
                    "维修",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0;

            if (!isUrgent)
            {
                return null;
            }

            string sn =
                GetCsvFieldByIndex(
                    fields,
                    CsvSnColumnIndex
                );

            string trackingNumber =
                GetCsvFieldByIndex(
                    fields,
                    CsvTrackingColumnIndex
                );

            string remark =
                GetCsvFieldByIndex(
                    fields,
                    CsvRemarkColumnIndex
                );

            if (string.IsNullOrWhiteSpace(sn) &&
                string.IsNullOrWhiteSpace(
                    trackingNumber
                ))
            {
                throw new InvalidOperationException(
                    "CSV 第 "
                    + rowNumber.ToString()
                    + " 行的 Y 列 SN 和 AD 列运单号不能同时为空。"
                );
            }

            /*
             * 备注允许为空。
             * 空备注时仍然可以显示“紧急”，只是不打印备注内容。
             */
            WorkOrderRemark item =
                new WorkOrderRemark();

            item.Id =
                "CSV-"
                + rowNumber.ToString()
                + "-"
                + Guid.NewGuid().ToString("N");

            item.Sn =
                string.IsNullOrWhiteSpace(sn)
                    ? null
                    : sn.Trim();

            item.TrackingNumber =
                string.IsNullOrWhiteSpace(
                    trackingNumber
                )
                    ? null
                    : trackingNumber.Trim();

            item.Remark =
                string.IsNullOrWhiteSpace(remark)
                    ? string.Empty
                    : remark.Trim();

            item.IsUrgent = true;

            item.RemarkTimestamp =
                DateTimeOffset.UtcNow
                    .ToUnixTimeSeconds();

            return item;
        }
        private string GetCsvFieldByIndex(
    string[] fields,
    int index)
        {
            if (fields == null)
            {
                return string.Empty;
            }

            if (index < 0 ||
                index >= fields.Length)
            {
                return string.Empty;
            }

            string value = fields[index];

            if (value == null)
            {
                return string.Empty;
            }

            return value
                .Trim()
                .TrimStart('\uFEFF');
        }
        private bool IsCsvHeaderRow(
    string[] fields)
        {
            string yValue =
                GetCsvFieldByIndex(
                    fields,
                    CsvSnColumnIndex
                );

            string adValue =
                GetCsvFieldByIndex(
                    fields,
                    CsvTrackingColumnIndex
                );

            string arValue =
                GetCsvFieldByIndex(
                    fields,
                    CsvRemarkColumnIndex
                );

            if (yValue.IndexOf(
                    "SN",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0)
            {
                return true;
            }

            if (yValue.Contains("序列号") ||
                yValue.Contains("编码"))
            {
                return true;
            }

            if (adValue.Contains("运单") ||
                adValue.IndexOf(
                    "Tracking",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0)
            {
                return true;
            }

            if (arValue.Contains("备注"))
            {
                return true;
            }

            return false;
        }
        private void AddOrReplaceManualWorkOrder(
    WorkOrderRemark newItem)
        {
            WorkOrderRemark existing =
                null;

            foreach (
                WorkOrderRemark item
                in _workOrderRemarks
            )
            {
                if (item == null)
                {
                    continue;
                }

                bool sameSn =
                    !string.IsNullOrWhiteSpace(
                        newItem.Sn
                    )
                    &&
                    !string.IsNullOrWhiteSpace(
                        item.Sn
                    )
                    &&
                    string.Equals(
                        newItem.Sn.Trim(),
                        item.Sn.Trim(),
                        StringComparison.OrdinalIgnoreCase
                    );

                bool sameTracking =
                    !string.IsNullOrWhiteSpace(
                        newItem.TrackingNumber
                    )
                    &&
                    !string.IsNullOrWhiteSpace(
                        item.TrackingNumber
                    )
                    &&
                    string.Equals(
                        newItem.TrackingNumber.Trim(),
                        item.TrackingNumber.Trim(),
                        StringComparison.OrdinalIgnoreCase
                    );

                if (sameSn || sameTracking)
                {
                    existing = item;
                    break;
                }
            }

            if (existing != null)
            {
                _workOrderRemarks.Remove(
                    existing
                );
            }

            /*
             * 插入到最前面，匹配时人工数据优先。
             */
            _workOrderRemarks.Insert(
                0,
                newItem
            );
        }
        private Dictionary<string, int> CreateCsvHeaderMap(string[] headers)
        {
            Dictionary<string, int> map =
                new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase
                );

            int i;

            for (i = 0; i < headers.Length; i++)
            {
                string header =
                    headers[i] == null
                        ? string.Empty
                        : headers[i].Trim();

                /*
                 * 处理 UTF-8 BOM。
                 */
                header =
                    header.TrimStart('\uFEFF');

                if (!string.IsNullOrWhiteSpace(header))
                {
                    map[header] = i;
                }
            }

            return map;
        }
        private void ValidateCsvHeaders(
    Dictionary<string, int> headerMap)
        {
            if (!headerMap.ContainsKey("sn") &&
                !headerMap.ContainsKey(
                    "tracking_number"
                ))
            {
                throw new InvalidOperationException(
                    "CSV 必须包含 sn 或 tracking_number 字段。"
                );
            }

            if (!headerMap.ContainsKey("remark"))
            {
                throw new InvalidOperationException(
                    "CSV 必须包含 remark 字段。"
                );
            }
        }
        private WorkOrderRemark
    CreateWorkOrderFromCsv(
        string[] fields,
        Dictionary<string, int> headerMap,
        int rowNumber)
        {
            string sn =
                GetCsvValue(
                    fields,
                    headerMap,
                    "sn"
                );

            string trackingNumber =
                GetCsvValue(
                    fields,
                    headerMap,
                    "tracking_number"
                );

            string remark =
                GetCsvValue(
                    fields,
                    headerMap,
                    "remark"
                );

            string urgentText =
                GetCsvValue(
                    fields,
                    headerMap,
                    "is_urgent"
                );

            if (string.IsNullOrWhiteSpace(sn) &&
                string.IsNullOrWhiteSpace(
                    trackingNumber
                ))
            {
                throw new InvalidOperationException(
                    "CSV 第 "
                    + rowNumber.ToString()
                    + " 行的 sn 和 tracking_number 不能同时为空。"
                );
            }

            if (string.IsNullOrWhiteSpace(remark))
            {
                throw new InvalidOperationException(
                    "CSV 第 "
                    + rowNumber.ToString()
                    + " 行的 remark 不能为空。"
                );
            }

            bool isUrgent = true;

            if (!string.IsNullOrWhiteSpace(
                    urgentText
                ))
            {
                isUrgent =
                    ParseCsvBoolean(
                        urgentText,
                        rowNumber
                    );
            }

            WorkOrderRemark item =
                new WorkOrderRemark();

            item.Id =
                "CSV-"
                + Guid.NewGuid().ToString("N");

            item.Sn =
                string.IsNullOrWhiteSpace(sn)
                    ? null
                    : sn.Trim();

            item.TrackingNumber =
                string.IsNullOrWhiteSpace(
                    trackingNumber
                )
                    ? null
                    : trackingNumber.Trim();

            item.Remark =
                remark.Trim();

            item.IsUrgent =
                isUrgent;

            item.RemarkTimestamp =
                DateTimeOffset.UtcNow
                    .ToUnixTimeSeconds();

            return item;
        }
        private string GetCsvValue(
    string[] fields,
    Dictionary<string, int> headerMap,
    string columnName)
        {
            int index;

            if (!headerMap.TryGetValue(
                    columnName,
                    out index
                ))
            {
                return string.Empty;
            }

            if (index < 0 ||
                index >= fields.Length)
            {
                return string.Empty;
            }

            return fields[index] == null
                ? string.Empty
                : fields[index].Trim();
        }

        private void ImportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog =
        new OpenFileDialog();

            dialog.Title =
                "选择紧急工单 CSV 文件";

            dialog.Filter =
                "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*";

            dialog.Multiselect = false;

            bool? result =
                dialog.ShowDialog(this);

            if (result != true)
            {
                FocusScannerInput();
                return;
            }

            try
            {
                int importedCount =
                    ImportUrgentCsv(
                        dialog.FileName
                    );

                SetStatus(
                    "CSV 导入完成，共导入 "
                    + importedCount.ToString()
                    + " 条紧急工单。",
                    false
                );

                MessageBox.Show(
                    "成功导入 "
                    + importedCount.ToString()
                    + " 条紧急工单。",
                    "导入完成",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                SetStatus(
                    "CSV 导入失败："
                    + ex.Message,
                    true
                );

                MessageBox.Show(
                    ex.Message,
                    "CSV 导入失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                FocusScannerInput();
            }
        }
    }
}