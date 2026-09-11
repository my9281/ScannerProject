using Scanner.Helpers.Services;
using System.Windows;
using System.Windows.Controls;
namespace Scanner.WPF.Controllers
{
    public interface IChecklistWindowView
    {
        Window OwnerWindow { get; }
        Button ExportButton { get; }
        Button ExportSimplifiedButton { get; }
        Button ExportCurrentMonthButton { get; }
        DataGrid StatusSummaryDataGrid { get; }
        DataGridTextColumn CurrentMonthColumn { get; }
        DataGridTextColumn PreviousMonthColumn { get; }
        DataGridTextColumn TwoMonthsAgoColumn { get; }
        TextBlock BaseStatusTextBlock { get; }
        TextBlock BaseFileTextBlock { get; }
        TextBlock SnStatusTextBlock { get; }
        TextBlock SnFileTextBlock { get; }
    }
}
