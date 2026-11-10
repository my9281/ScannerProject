using Scanner.Controllers;
using Scanner.WPF.Controllers;
using System.Windows;
namespace Scanner.WPF
{
    public partial class ChecklistWindow : Window, IChecklistWindowView
    {
        private readonly ChecklistWindowController _controller;
        public ChecklistWindow(IControllerFactory<IChecklistWindowView, ChecklistWindowController> factory)
        {
            InitializeComponent();
            _controller = factory.Create(this);
            Closed += (s, e) => _controller.Dispose();
        }
        Window IChecklistWindowView.OwnerWindow => this;
        System.Windows.Controls.Button IChecklistWindowView.ExportButton => ExportButton;
        System.Windows.Controls.Button IChecklistWindowView.ExportSimplifiedButton => ExportSimplifiedButton;
        System.Windows.Controls.Button IChecklistWindowView.ExportCurrentMonthButton => ExportCurrentMonthButton;
        System.Windows.Controls.Button IChecklistWindowView.TemporaryFeature1Button => TemporaryFeature1Button;
        System.Windows.Controls.DataGrid IChecklistWindowView.StatusSummaryDataGrid => StatusSummaryDataGrid;
        System.Windows.Controls.DataGridTextColumn IChecklistWindowView.CurrentMonthColumn => CurrentMonthColumn;
        System.Windows.Controls.DataGridTextColumn IChecklistWindowView.PreviousMonthColumn => PreviousMonthColumn;
        System.Windows.Controls.DataGridTextColumn IChecklistWindowView.TwoMonthsAgoColumn => TwoMonthsAgoColumn;
        System.Windows.Controls.TextBlock IChecklistWindowView.BaseStatusTextBlock => BaseStatusTextBlock;
        System.Windows.Controls.TextBlock IChecklistWindowView.BaseFileTextBlock => BaseFileTextBlock;
        System.Windows.Controls.TextBlock IChecklistWindowView.SnStatusTextBlock => SnStatusTextBlock;
        System.Windows.Controls.TextBlock IChecklistWindowView.SnFileTextBlock => SnFileTextBlock;
        private void ImportSnButton_Click(object sender, RoutedEventArgs e) => _controller.ImportSnButton_Click(sender, e);
        private void ExportButton_Click(object sender, RoutedEventArgs e) => _controller.ExportButton_Click(sender, e);
        private void ExportSimplifiedButton_Click(object sender, RoutedEventArgs e) => _controller.ExportSimplifiedButton_Click(sender, e);
        private void ExportCurrentMonthButton_Click(object sender, RoutedEventArgs e) => _controller.ExportCurrentMonthButton_Click(sender, e);
        private void TemporaryFeature1Button_Click(object sender, RoutedEventArgs e) => _controller.TemporaryFeature1Button_Click(sender, e);
    }
}
