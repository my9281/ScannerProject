using System.Windows;
using System.Windows.Controls;
namespace Scanner.PlatformControllers
{
    public interface IOutboundInspectionWindowView
    {
        Window OwnerWindow { get; }
        TextBlock TextFileTextBlock { get; }
        TextBlock BaseFileTextBlock { get; }
        TextBox PalletNumberTextBox { get; }
        Button PrintButton { get; }
        Button ExportButton { get; }
        TextBlock SummaryTextBlock { get; }
        DataGrid ResultDataGrid { get; }
    }
}
