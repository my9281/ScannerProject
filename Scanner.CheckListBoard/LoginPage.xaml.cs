using Scanner.CheckListBoard.Services;

namespace Scanner.CheckListBoard;

public partial class LoginPage : ContentPage
{
    private readonly AccountClient accounts;
    private bool loaded;
    private bool busy;
    public LoginPage(AccountClient accounts)
    {
        this.accounts = accounts;
        InitializeComponent();
        ServerInput.Text = accounts.ServerAddress;
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (loaded) return;
        loaded = true;
        LoginButton.IsEnabled = false;
        try
        {
            var saved = await AccountStorage.ReadAsync();
            if (saved is not null && saved.ServerAddress == accounts.ServerAddress)
            {
                UsernameInput.Text = saved.Username;
                PasswordInput.Text = saved.Password;
            }
        }
        catch (Exception)
        {
            ShowStatus("无法读取已保存的账号，请重新输入。", false);
        }
        finally { LoginButton.IsEnabled = true; }
    }
    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        if (busy) return;
        busy = true;
        LoginButton.IsEnabled = false;
        UsernameInput.IsEnabled = PasswordInput.IsEnabled = ServerInput.IsEnabled = RememberInput.IsEnabled = false;
        BusyIndicator.IsVisible = BusyIndicator.IsRunning = true;
        StatusLabel.IsVisible = false;
        try
        {
            string address = AccountClient.ValidateServer(ServerInput.Text ?? "").AbsoluteUri;
            if (address != accounts.ServerAddress) AccountStorage.Clear();
            accounts.ServerAddress = address;
            var session = await accounts.LoginAsync(UsernameInput.Text ?? "", PasswordInput.Text ?? "");
            string? storageMessage = null;
            try
            {
                if (RememberInput.IsChecked)
                    await AccountStorage.SaveAsync(new SavedAccount(UsernameInput.Text!.Trim(), PasswordInput.Text!, address));
                else AccountStorage.Clear();
            }
            catch (Exception) { storageMessage = "登录成功，但账号密码未能保存；下次启动需重新输入。"; }
            PasswordInput.Text = string.Empty;
            if (storageMessage is not null) await DisplayAlertAsync("设备存储", storageMessage, "继续");
            if (Window is { } window) window.Page = new AppShell(accounts);
        }
        catch (HttpRequestException) { ShowStatus("无法连接登录服务，请检查网络和服务地址。"); }
        catch (TaskCanceledException) { ShowStatus("登录请求超时，请稍后重试。"); }
        catch (Exception exception)
        {
            ShowStatus(exception is ArgumentException or InvalidOperationException ? exception.Message : "登录处理失败，请稍后重试。");
        }
        finally
        {
            busy = false;
            LoginButton.IsEnabled = true;
            UsernameInput.IsEnabled = PasswordInput.IsEnabled = ServerInput.IsEnabled = RememberInput.IsEnabled = true;
            BusyIndicator.IsVisible = BusyIndicator.IsRunning = false;
        }
    }
    private void ShowStatus(string text, bool error = true)
    {
        StatusLabel.Text = text;
        StatusLabel.TextColor = Color.FromArgb(error ? "#E84949" : "#D9B866");
        StatusLabel.IsVisible = true;
        SemanticScreenReader.Default.Announce(text);
    }
    private void OnUsernameCompleted(object? sender, EventArgs e) => PasswordInput.Focus();
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (FormGrid is null) return;
        bool compact = width < 760;
        FormGrid.ColumnDefinitions[0].Width = compact ? new GridLength(24) : new GridLength(1, GridUnitType.Star);
        FormGrid.ColumnDefinitions[1].Width = new GridLength(compact ? 1 : 4, GridUnitType.Star);
        FormGrid.ColumnDefinitions[2].Width = compact ? new GridLength(24) : new GridLength(5, GridUnitType.Star);
    }
    private void OnRevealClicked(object? sender, EventArgs e)
    {
        PasswordInput.IsPassword = !PasswordInput.IsPassword;
        RevealButton.Text = PasswordInput.IsPassword ? "显示" : "隐藏";
    }
    private void OnSettingsClicked(object? sender, EventArgs e) => ServerSettings.IsVisible = !ServerSettings.IsVisible;
    private void OnRememberChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (!e.Value)
        {
            try { AccountStorage.Clear(); }
            catch (Exception) { ShowStatus("无法清除已保存账号，请检查设备安全存储。"); }
        }
    }
}
