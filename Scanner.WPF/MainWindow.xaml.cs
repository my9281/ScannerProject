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
            Closed += (s, e) => _controller.Dispose();
        }
        Window IMainWindowView.OwnerWindow => this;
        System.Windows.Controls.TextBlock IMainWindowView.OperatorTextBlock => OperatorTextBlock;
        System.Windows.Controls.TextBox IMainWindowView.SnTextBox => SnTextBox;
        System.Windows.Controls.Button IMainWindowView.OpenLogButton => OpenLogButton;
        System.Windows.Controls.Button IMainWindowView.UploadLogButton => UploadLogButton;
        System.Windows.Controls.Button IMainWindowView.PrintButton => PrintButton;
        System.Windows.Controls.TextBlock IMainWindowView.PrinterTextBlock => PrinterTextBlock;
        System.Windows.Controls.TextBlock IMainWindowView.LogFileTextBlock => LogFileTextBlock;
        System.Windows.Controls.TextBlock IMainWindowView.CountTextBlock => CountTextBlock;
        System.Windows.Controls.TextBlock IMainWindowView.StatusTextBlock => StatusTextBlock;
        System.Windows.Controls.TextBlock IMainWindowView.WebStatus => WebStatus;
        System.Windows.Controls.TextBlock IMainWindowView.WebLastTime => WebLastTime;
        System.Windows.Controls.Button IMainWindowView.GlobalBaseButton => GlobalBaseButton;
        System.Windows.Controls.Button IMainWindowView.ImportUrgentWorkOrdersButton => ImportUrgentWorkOrdersButton;
        private void Window_Loaded(object sender, RoutedEventArgs e) => _controller.Window_Loaded(sender, e);
        private void ScanTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => _controller.ScanTextBox_GotKeyboardFocus(sender, e);
        private void ImportGlobalBase_Click(object sender, RoutedEventArgs e) => _controller.ImportGlobalBase_Click(sender, e);
        private void OpenLocationFeeComparisonButton_Click(object sender, RoutedEventArgs e) => _controller.OpenLocationFeeComparisonButton_Click(sender, e);
        private void OpenOutboundInspectionButton_Click(object sender, RoutedEventArgs e) => _controller.OpenOutboundInspectionButton_Click(sender, e);
        private void OpenChecklistButton_Click(object sender, RoutedEventArgs e) => _controller.OpenChecklistButton_Click(sender, e);
        private void CreateDailyReportButton_Click(object sender, RoutedEventArgs e) => _controller.CreateDailyReportButton_Click(sender, e);
    }
}
