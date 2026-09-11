using Microsoft.Extensions.DependencyInjection;

namespace Scanner.MaUI;

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
        _services.GetRequiredService<Scanner.MaUI.Windows.IWindow>().ShowMain();
    }

    public void ShowLoginPage()
    {
        _services.GetRequiredService<Scanner.MaUI.Windows.IWindow>().ShowLogin();
    }
}
