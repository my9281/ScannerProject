using Microsoft.Maui.Controls.Shapes;
using Scanner.CheckListBoard.Services;

namespace Scanner.CheckListBoard;

public partial class MainPage : ContentPage
{
    private readonly AccountClient accounts;
    public MainPage(AccountClient accounts)
    {
        this.accounts = accounts;
        InitializeComponent();
        var session = accounts.Session ?? throw new InvalidOperationException("请先登录。");
        IdentityLabel.Text = $"{session.DisplayName ?? session.Username}，欢迎回来";
        RoleLabel.Text = session.Message;
        PermissionLabel.Text = session.Role switch
        {
            "admin" => "管理员 / 检测查看 · 检测操作 · 系统配置",
            "user" => "普通用户 / 检测查看 · 检测操作",
            _ => "只读用户 / 检测查看"
        };
        string[] modules = ["入库检测", "安全检测", "裸机检测", "整机检测", "查看评级", "历史查看", "系统配置", "工单查看"];
        string[] subtitles = ["来料验收", "安全核验", "单机检查", "整机验证", "品质分级", "检测记录", "工作台设置", "工单列表"];
        for (int row = 0; row < 4; row++) ModuleGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (int index = 0; index < modules.Length; index++)
        {
            string title = modules[index];
            string permission = index == 6 ? "system.configure" : index < 4 ? "checklist.write" : "checklist.read";
            bool allowed = accounts.HasPermission(permission);
            var content = new VerticalStackLayout
            {
                Padding = new Thickness(18, 16), Spacing = 9, InputTransparent = true,
                Children =
                {
                    new Label { Text = $"{index + 1:00}", FontSize = 15, TextColor = Color.FromArgb(index == 6 ? "#D9B866" : "#8FCFDA") },
                    new Label { Text = title, FontSize = 18, TextColor = Colors.White },
                    new Label { Text = allowed ? subtitles[index] : "当前角色无操作权限", FontSize = 11, TextColor = Color.FromArgb(allowed ? "#929EAA" : "#E84949") }
                }
            };
            var layers = new Grid();
            layers.Add(content);
            var button = new Button { BackgroundColor = Colors.Transparent, Text = title, TextColor = Colors.Transparent, BorderWidth = 0, CornerRadius = 8, Padding = 0, IsEnabled = allowed };
            SemanticProperties.SetDescription(button, title);
            button.Clicked += async (_, _) =>
            {
                if (!accounts.HasPermission(permission))
                {
                    await DisplayAlertAsync("登录状态", "登录已过期或当前账户无此权限，请重新登录。", "返回登录");
                    await LogoutAsync();
                    return;
                }
                if (title == "工单查看")
                {
                    await Navigation.PushAsync(new WorkOrdersPage(accounts));
                    return;
                }
                await DisplayAlertAsync(title, "此功能页面待接入。", "返回工作台");
            };
            layers.Add(button);
            var card = new Border
            {
                Stroke = new SolidColorBrush(Color.FromArgb("#408FCFDA")), StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(8) },
                BackgroundColor = Color.FromArgb("#99000000"), Content = layers, Opacity = allowed ? 1 : 0.55
            };
            ModuleGrid.Add(card, index % 2, index / 2);
        }
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (accounts.Session is null || accounts.Session.ExpiresAt <= DateTimeOffset.UtcNow)
            Dispatcher.Dispatch(() => { if (Window is { } window) window.Page = new LoginPage(accounts); });
    }
    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        LogoutButton.IsEnabled = false;
        await LogoutAsync();
    }
    private async Task LogoutAsync()
    {
        try { await accounts.LogoutAsync(); }
        catch (Exception) { await DisplayAlertAsync("已退出此设备", "网络不可用，服务端会话将按有效期到期。", "确定"); }
        finally { if (Window is { } window) window.Page = new LoginPage(accounts); }
    }
}
