using Scanner.Controllers;
using Scanner.MaUI.Controllers;
using Scanner.MaUI.Views;
namespace Scanner.MaUI;

public partial class LoginPage : ContentPage, ILoginPageView
{
    private readonly LoginPageController _controller;
    public LoginPage(IControllerFactory<ILoginPageView, LoginPageController> factory)
    {
        InitializeComponent();
        _controller = factory.Create(this);
    }
    Entry ILoginPageView.UsernameEntry => UsernameEntry;
    Entry ILoginPageView.PasswordEntry => PasswordEntry;
    CheckBox ILoginPageView.RememberCheckBox => RememberCheckBox;
    Button ILoginPageView.LoginButton => LoginButton;
    Button ILoginPageView.LocalButton => LocalButton;
    ActivityIndicator ILoginPageView.BusyIndicator => BusyIndicator;
    Label ILoginPageView.StatusLabel => StatusLabel;
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _controller.AppearingAsync(); }
        catch (Exception ex) { await DisplayAlertAsync("加载失败", ex.Message, "确定"); }
    }
    protected override void OnDisappearing() { _controller.Dispose(); base.OnDisappearing(); }
    private void LanguageButton_Clicked(object? sender, EventArgs e) => _controller.LanguageButton_Clicked(sender, e);
    private void UsernameEntry_Completed(object? sender, EventArgs e) => _controller.UsernameEntry_Completed(sender, e);
    private void PasswordEntry_Completed(object? sender, EventArgs e) => _controller.PasswordEntry_Completed(sender, e);
    private void LoginButton_Clicked(object? sender, EventArgs e) => _controller.LoginButton_Clicked(sender, e);
    private void LocalButton_Clicked(object? sender, EventArgs e) => _controller.LocalButton_Clicked(sender, e);
}
