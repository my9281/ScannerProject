using System.Windows;
using System.Windows.Controls;
namespace Scanner.PlatformControllers
{
    public interface IMainWindowView
    {
        Window OwnerWindow { get; }
        TextBlock OperatorTextBlock { get; }
        TextBox SnTextBox { get; }
        Button OpenLogButton { get; }
        Button UploadLogButton { get; }
        Button PrintButton { get; }
        TextBlock PrinterTextBlock { get; }
        TextBlock LogFileTextBlock { get; }
        TextBlock CountTextBlock { get; }
        TextBlock StatusTextBlock { get; }
        TextBlock WebStatus { get; }
        TextBlock WebLastTime { get; }
        Button GlobalBaseButton { get; }
        Button ImportUrgentWorkOrdersButton { get; }
    }
}
