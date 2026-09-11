using Scanner.Controllers;
using Scanner.MaUI.Controllers;
using Scanner.MaUI.Views;
namespace Scanner.MaUI;
public partial class OperationsPage : ContentPage, IOperationsPageView
{
    private readonly OperationsPageController _controller;
    public OperationsPage(IControllerFactory<IOperationsPageView, OperationsPageController> factory)
    {
        InitializeComponent();
        _controller = factory.Create(this);
    }
    Label IOperationsPageView.GlobalBaseLabel => GlobalBaseLabel;
    Label IOperationsPageView.InboundSnLabel => InboundSnLabel;
    Label IOperationsPageView.InboundStatusLabel => InboundStatusLabel;
    Label IOperationsPageView.OutboundTextLabel => OutboundTextLabel;
    Label IOperationsPageView.OutboundStatusLabel => OutboundStatusLabel;
    Label IOperationsPageView.FeeTemplateLabel => FeeTemplateLabel;
    Label IOperationsPageView.FeeStatusLabel => FeeStatusLabel;
    private void PickInboundBase_Clicked(object? sender, EventArgs e) => _controller.PickInboundBase_Clicked(sender, e);
    private void PickInboundSn_Clicked(object? sender, EventArgs e) => _controller.PickInboundSn_Clicked(sender, e);
    private void ExportInboundMatch_Clicked(object? sender, EventArgs e) => _controller.ExportInboundMatch_Clicked(sender, e);
    private void ExportCurrentMonth_Clicked(object? sender, EventArgs e) => _controller.ExportCurrentMonth_Clicked(sender, e);
    private void PickOutboundText_Clicked(object? sender, EventArgs e) => _controller.PickOutboundText_Clicked(sender, e);
    private void ExportOutbound_Clicked(object? sender, EventArgs e) => _controller.ExportOutbound_Clicked(sender, e);
    private void PickFeeTemplate_Clicked(object? sender, EventArgs e) => _controller.PickFeeTemplate_Clicked(sender, e);
    private void ExportFee_Clicked(object? sender, EventArgs e) => _controller.ExportFee_Clicked(sender, e);
}
