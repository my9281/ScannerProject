using MaUIScanner.Models;
using MaUIScanner.Services;

namespace MaUIScanner;

public partial class LoginPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly SessionStore _sessions;
    private bool _loaded;
    public LoginPage(AuthService auth, SessionStore sessions) { InitializeComponent(); _auth = auth; _sessions = sessions; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;
        _loaded = true;
        AuthSession? session = await _sessions.RestoreAsync();
        if (session != null) { ShowMain(); return; }
        (string user, string password) = await _sessions.GetRememberedLoginAsync();
        UsernameEntry.Text = user; PasswordEntry.Text = password; RememberCheckBox.IsChecked = !string.IsNullOrEmpty(password);
        (string.IsNullOrEmpty(user) ? UsernameEntry : PasswordEntry).Focus();
    }

    private void UsernameEntry_Completed(object? sender, EventArgs e) => PasswordEntry.Focus();
    private async void PasswordEntry_Completed(object? sender, EventArgs e) => await LoginAsync();
    private async void LoginButton_Clicked(object? sender, EventArgs e) => await LoginAsync();
    private async void LocalButton_Clicked(object? sender, EventArgs e)
    {
        if (BusyIndicator.IsRunning) return;
        await _sessions.SaveSessionAsync(new AuthSession { Operator = "本地扫描", Role = "本地模式", IsLocalMode = true, ExpiresAt = DateTime.MaxValue });
        ShowMain();
    }

    private async Task LoginAsync()
    {
        if (BusyIndicator.IsRunning) return;
        SetBusy(true); StatusLabel.Text = "正在登录……";
        try
        {
            AuthSession session = await _auth.LoginAsync(UsernameEntry.Text ?? string.Empty, PasswordEntry.Text ?? string.Empty);
            await _sessions.SaveSessionAsync(session);
            await _sessions.SaveRememberedLoginAsync(UsernameEntry.Text ?? string.Empty, PasswordEntry.Text ?? string.Empty, RememberCheckBox.IsChecked);
            ShowMain();
        }
        catch (Exception ex) { StatusLabel.Text = ex.Message; }
        finally { SetBusy(false); }
    }

    private void SetBusy(bool busy) { BusyIndicator.IsVisible = busy; BusyIndicator.IsRunning = busy; LoginButton.IsEnabled = !busy; LocalButton.IsEnabled = !busy; }
    private static void ShowMain() => ((App)Application.Current!).ShowMainPage();
}
