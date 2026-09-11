namespace MaUIScanner.Views;
public interface IOperationsPageView
{
    bool IsBusy { get; set; }
    Label GlobalBaseLabel { get; }
    Label InboundSnLabel { get; }
    Label InboundStatusLabel { get; }
    Label OutboundTextLabel { get; }
    Label OutboundStatusLabel { get; }
    Label FeeTemplateLabel { get; }
    Label FeeStatusLabel { get; }
}
