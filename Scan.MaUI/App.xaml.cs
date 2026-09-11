using Microsoft.Extensions.DependencyInjection;

namespace Scan.MaUI;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
        Services.LocalizationService.Current.Change("zh-CN");
    }
    protected override Window CreateWindow(IActivationState? activationState)
    {
        LoginPage page = _services.GetRequiredService<LoginPage>();
        return new Window(new NavigationPage(page)) { Title = "YM-Star Scanner System" };
    }

    public void ShowMainPage()
    {
        _services.GetRequiredService<Scan.MaUI.Windows.IWindow>().ShowMain();
    }

    public void ShowLoginPage()
    {
        _services.GetRequiredService<Scan.MaUI.Windows.IWindow>().ShowLogin();
    }
}
