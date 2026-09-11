using System.Windows;
using System.Windows.Controls;
namespace Scanner.PlatformControllers
{
    public interface ILocationFeeComparisonWindowView
    {
        Window OwnerWindow { get; }
        TextBlock TemplateFileTextBlock { get; }
        TextBlock BaseFileTextBlock { get; }
        TextBlock StatusTextBlock { get; }
        Button ExportButton { get; }
    }
}
