using Scanner.Controllers;
using Scanner.WPF.Controllers;
using System.Windows;
namespace Scanner.WPF
{
    public partial class LocationFeeComparisonWindow : Window, ILocationFeeComparisonWindowView
    {
        private readonly LocationFeeComparisonWindowController _controller;
        public LocationFeeComparisonWindow(IControllerFactory<ILocationFeeComparisonWindowView, LocationFeeComparisonWindowController> factory)
        {
            InitializeComponent();
            _controller = factory.Create(this);
            Closed += (s, e) => _controller.Dispose();
        }
        Window ILocationFeeComparisonWindowView.OwnerWindow => this;
        System.Windows.Controls.TextBlock ILocationFeeComparisonWindowView.TemplateFileTextBlock => TemplateFileTextBlock;
        System.Windows.Controls.TextBlock ILocationFeeComparisonWindowView.BaseFileTextBlock => BaseFileTextBlock;
        System.Windows.Controls.TextBlock ILocationFeeComparisonWindowView.StatusTextBlock => StatusTextBlock;
        System.Windows.Controls.Button ILocationFeeComparisonWindowView.ExportButton => ExportButton;
        private void SelectTemplateButton_Click(object sender, RoutedEventArgs e) => _controller.SelectTemplateButton_Click(sender, e);
        private void ExportButton_Click(object sender, RoutedEventArgs e) => _controller.ExportButton_Click(sender, e);
    }
}
