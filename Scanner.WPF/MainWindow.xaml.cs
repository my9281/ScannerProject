using Scanner.Controllers;
using Scanner.WPF.Controllers;
using System.Windows;
using System.Windows.Input;
namespace Scanner.WPF
{
    public partial class MainWindow : Window, IMainWindowView
    {
        private readonly MainWindowController _controller;
        public MainWindow(IControllerFactory<IMainWindowView, MainWindowController> factory)
        {
            InitializeComponent();
            _controller = factory.Create(this);
            ScanContent.ScanInputFocused += ScanTextBox_GotKeyboardFocus;
            Closed += (s, e) => _controller.Dispose();
        }
        Window IMainWindowView.OwnerWindow => this;
        bool IMainWindowView.IsScanViewActive => ScanPanel.Visibility == Visibility.Visible;
        System.Windows.Controls.TextBlock IMainWindowView.OperatorTextBlock => ScanContent.OperatorTextBlock;
        System.Windows.Controls.TextBox IMainWindowView.SnTextBox => ScanContent.SnTextBox;
        System.Windows.Controls.Button IMainWindowView.OpenLogButton => ScanContent.OpenLogButton;
        System.Windows.Controls.Button IMainWindowView.UploadLogButton => ScanContent.UploadLogButton;
        System.Windows.Controls.Button IMainWindowView.PrintButton => ScanContent.PrintButton;
        System.Windows.Controls.TextBlock IMainWindowView.PrinterTextBlock => ScanContent.PrinterTextBlock;
        System.Windows.Controls.TextBlock IMainWindowView.LogFileTextBlock => ScanContent.LogFileTextBlock;
        System.Windows.Controls.TextBlock IMainWindowView.CountTextBlock => ScanContent.CountTextBlock;
        System.Windows.Controls.TextBlock IMainWindowView.StatusTextBlock => ScanContent.StatusTextBlock;
        System.Windows.Controls.TextBlock IMainWindowView.WebStatus => ScanContent.WebStatus;
        System.Windows.Controls.TextBlock IMainWindowView.WebLastTime => ScanContent.WebLastTime;
        System.Windows.Controls.Button IMainWindowView.GlobalBaseButton => GlobalBaseButton;
        System.Windows.Controls.Button IMainWindowView.ImportUrgentWorkOrdersButton => ImportUrgentWorkOrdersButton;
        private void Window_Loaded(object sender, RoutedEventArgs e) => _controller.Window_Loaded(sender, e);
        private void ScanTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => _controller.ScanTextBox_GotKeyboardFocus(sender, e);
        private void ImportGlobalBase_Click(object sender, RoutedEventArgs e) => _controller.ImportGlobalBase_Click(sender, e);
        private void OpenLocationFeeComparisonButton_Click(object sender, RoutedEventArgs e) => _controller.OpenLocationFeeComparisonButton_Click(sender, e);
        private void OpenOutboundInspectionButton_Click(object sender, RoutedEventArgs e) => _controller.OpenOutboundInspectionButton_Click(sender, e);
        private void OpenChecklistButton_Click(object sender, RoutedEventArgs e) => _controller.OpenChecklistButton_Click(sender, e);
        private void CreateDailyReportButton_Click(object sender, RoutedEventArgs e) => _controller.CreateDailyReportButton_Click(sender, e);
        private void OpenScanView_Click(object sender, RoutedEventArgs e)
        {
            DirectoryPanel.Visibility = Visibility.Collapsed;
            ReplacementPanel.Visibility = Visibility.Collapsed;
            ScanPanel.Visibility = Visibility.Visible;
            _controller.ActivateScanner();
        }
        private void OpenReplacementView_Click(object sender, RoutedEventArgs e)
        {
            DirectoryPanel.Visibility = Visibility.Collapsed;
            ScanPanel.Visibility = Visibility.Collapsed;
            SkuSerialLabelPanel.Visibility = Visibility.Collapsed;
            ReplacementPanel.Visibility = Visibility.Visible;
        }
        private void OpenSkuSerialLabelView_Click(object sender, RoutedEventArgs e)
        {
            DirectoryPanel.Visibility = Visibility.Collapsed;
            ScanPanel.Visibility = Visibility.Collapsed;
            ReplacementPanel.Visibility = Visibility.Collapsed;
            SkuSerialLabelPanel.Visibility = Visibility.Visible;
        }
        private void BackToDirectory_Click(object sender, RoutedEventArgs e)
        {
            ScanPanel.Visibility = Visibility.Collapsed;
            ReplacementPanel.Visibility = Visibility.Collapsed;
            SkuSerialLabelPanel.Visibility = Visibility.Collapsed;
            DirectoryPanel.Visibility = Visibility.Visible;
        }
    }
}
