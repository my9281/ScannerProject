using Microsoft.Extensions.DependencyInjection;

namespace MaUIScanner;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }
    protected override Window CreateWindow(IActivationState? activationState)
    {
        LoginPage page = _services.GetRequiredService<LoginPage>();
        return new Window(new NavigationPage(page)) { Title = "YM-Star Scanner System" };
    }

    public void ShowMainPage()
    {
        MainPage page = _services.GetRequiredService<MainPage>();
        Windows[0].Page = new NavigationPage(page);
    }

    public void ShowLoginPage()
    {
        LoginPage page = _services.GetRequiredService<LoginPage>();
        Windows[0].Page = new NavigationPage(page);
    }
}
