using MaUIScanner.Models;
using MaUIScanner.Services;

using Scanner.Controllers;
using MaUIScanner.Views;
namespace MaUIScanner.Controllers;
public sealed class LoginPageController : ControllerBase
{
    private readonly ILoginPageView _view;
    private readonly MaUIScanner.Windows.IWindow _window;

    private readonly AuthService _auth;
    private readonly SessionStore _sessions;
    private bool _loaded;
    public LoginPageController(ILoginPageView view, MaUIScanner.Windows.IWindow window, AuthService auth, SessionStore sessions) { _view = view; _window = window;   _auth = auth; _sessions = sessions;  }

    public async Task AppearingAsync()
    {
        if (_loaded) return;
        _loaded = true;
        AuthSession? session = await _sessions.RestoreAsync();
        if (session != null) { ShowMain(); return; }
        (string user, string password) = await _sessions.GetRememberedLoginAsync();
        _view.UsernameEntry.Text = user; _view.PasswordEntry.Text = password; _view.RememberCheckBox.IsChecked = !string.IsNullOrEmpty(password);
        (string.IsNullOrEmpty(user) ? _view.UsernameEntry : _view.PasswordEntry).Focus();
    }

    public void UsernameEntry_Completed(object? sender, EventArgs e) => _view.PasswordEntry.Focus();
    public void LanguageButton_Clicked(object? sender, EventArgs e)
    {
        LocalizationService.Current.Change((string)((Button)sender!).CommandParameter);
        _view.StatusLabel.Text = string.Empty;
    }
    public async void PasswordEntry_Completed(object? sender, EventArgs e) => await LoginAsync();
    public async void LoginButton_Clicked(object? sender, EventArgs e) => await LoginAsync();
    public async void LocalButton_Clicked(object? sender, EventArgs e)
    {
        if (_view.BusyIndicator.IsRunning) return;
        await _sessions.SaveSessionAsync(new AuthSession { Operator = "本地扫描", Role = "本地模式", IsLocalMode = true, ExpiresAt = DateTime.MaxValue });
        ShowMain();
    }

    private async Task LoginAsync()
    {
        if (_view.BusyIndicator.IsRunning) return;
        SetBusy(true); _view.StatusLabel.Text = LocalizationService.Current.Get("SigningIn");
        try
        {
            AuthSession session = await _auth.LoginAsync(_view.UsernameEntry.Text ?? string.Empty, _view.PasswordEntry.Text ?? string.Empty);
            await _sessions.SaveSessionAsync(session);
            await _sessions.SaveRememberedLoginAsync(_view.UsernameEntry.Text ?? string.Empty, _view.PasswordEntry.Text ?? string.Empty, _view.RememberCheckBox.IsChecked);
            ShowMain();
        }
        catch (Exception ex) { _view.StatusLabel.Text = ex.Message; }
        finally { SetBusy(false); }
    }

    private void SetBusy(bool busy) { _view.BusyIndicator.IsVisible = busy; _view.BusyIndicator.IsRunning = busy; _view.LoginButton.IsEnabled = !busy; _view.LocalButton.IsEnabled = !busy; }
    private void ShowMain() => _window.ShowMain();

}
