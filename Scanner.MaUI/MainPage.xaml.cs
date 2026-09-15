using Scanner.Controllers;
using Scanner.MaUI.Controllers;
using Scanner.MaUI.Views;
namespace Scanner.MaUI;

public partial class MainPage : ContentPage, IMainPageView
{
    private readonly MainPageController _controller;
    public MainPage(IControllerFactory<IMainPageView, MainPageController> factory)
    {
        InitializeComponent();
        _controller = factory.Create(this);
    }
    Entry IMainPageView.ScanEntry => ScanEntry;
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _controller.AppearingAsync(); }
        catch (Exception ex) { await DisplayAlertAsync("加载失败", ex.Message, "确定"); }
    }
    protected override void OnDisappearing() { _controller.Dispose(); base.OnDisappearing(); }
    private void LanguageButton_Clicked(object? sender, EventArgs e) => _controller.LanguageButton_Clicked(sender, e);
    private void OperationsButton_Clicked(object? sender, EventArgs e) => _controller.OperationsButton_Clicked(sender, e);
}
