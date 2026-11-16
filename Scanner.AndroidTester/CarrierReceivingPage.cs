using System.Text.Json;
using Scanner.AndroidTester.Services;

namespace Scanner.AndroidTester;

public sealed class CarrierReceivingPage : ContentPage
{
    private const string SessionKey = "carrier-receiving-session-v1";
    private readonly Entry username = new() { Placeholder = "Web 收货账号", MaxLength = 100 };
    private readonly Entry password = new() { Placeholder = "密码（不保存）", IsPassword = true, MaxLength = 4096 };
    private readonly Entry barcode = new() { Placeholder = "扫描普通运单条码", ReturnType = ReturnType.Done, FontSize = 22 };
    private readonly Picker carrier = new() { Title = "承运商", ItemsSource = new[] { "FedEx", "USPS" }, SelectedIndex = 0 };
    private readonly Label scope = new() { TextColor = Colors.White };
    private readonly Label status = new() { TextColor = Colors.White };
    private readonly Label count = new() { TextColor = Colors.White };
    private readonly Button loginButton = new() { Text = "登录 / 刷新身份" };
    private readonly Button uploadButton = new() { Text = "上传待确认收货" };
    private readonly Button recordButton = new() { Text = "记录收货（离线可用）" };
    private readonly Button batchButton = new() { Text = "本批已上传，开始新批次" };
    private readonly Button logoutButton = new() { Text = "退出登录（保留待上传记录）" };
    private readonly CollectionView list = new() { SelectionMode = SelectionMode.None, HeightRequest = 240 };
    private readonly CarrierReceivingQueue queue;
    private readonly CarrierReceivingApi api;
    private readonly string server;
    private ReceivingLogin? session;
    private bool busy, active;

    public CarrierReceivingPage(CarrierReceivingQueue queue, CarrierReceivingApi api, AppSettings settings)
    {
        this.queue = queue; this.api = api; server = settings.UploadBaseUrl.AbsoluteUri;
        Title = "包裹收货"; BackgroundColor = Color.FromArgb("#080A0C");
        loginButton.Clicked += async (_, _) => await Run(async () =>
        {
            var result = await api.Login(username.Text?.Trim() ?? "", password.Text ?? "", CancellationToken.None);
            queue.Configure(result.Context);
            session = result;
            await SecureStorage.Default.SetAsync(SessionKey, JsonSerializer.Serialize(new SavedLogin(server, result)));
            password.Text = ""; status.Text = queue.Context?.SiteId == result.Context.SiteId ? "登录成功，站点已确认。" : "站点已变更：旧记录保持原站点，仅允许尝试上传确认，请联系管理员。";
        });
        recordButton.Clicked += async (_, _) => await Record(); barcode.Completed += async (_, _) => await Record();
        uploadButton.Clicked += async (_, _) => await Upload();
        batchButton.Clicked += async (_, _) => await Run(() => { queue.NewBatch(); status.Text = "已建立新收货批次。"; return Task.CompletedTask; });
        logoutButton.Clicked += (_, _) => { if (busy) return; SecureStorage.Default.Remove(SessionKey); session = null; password.Text = ""; status.Text = "已退出，待上传记录保留。"; Refresh(); };
        list.ItemTemplate = new DataTemplate(() => { var label = new Label { TextColor = Colors.White, Padding = 5 }; label.SetBinding(Label.TextProperty, "."); return label; });
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 18, Spacing = 12, Children = {
            new Label { Text = "使用 Web 普通用户或管理员账号；首次联网登录后可离线扫描。", TextColor = Colors.White },
            username, password, loginButton, logoutButton, scope, carrier, barcode, recordButton, count, status, uploadButton, batchButton, list } } };
        Refresh();
    }
    private sealed record SavedLogin(string Server, ReceivingLogin Login);
    protected override async void OnAppearing()
    {
        base.OnAppearing(); active = true;
        await Run(async () =>
        {
            string? saved = await SecureStorage.Default.GetAsync(SessionKey);
            if (saved is not null)
            {
                var stored = JsonSerializer.Deserialize<SavedLogin>(saved);
                if (stored?.Server == server)
                {
                    queue.Configure(stored.Login.Context); session = stored.Login; username.Text = session.Context.Username;
                    status.Text = session.ExpiresAt > DateTimeOffset.UtcNow ? "已恢复本机收货身份，可扫码；上传时会校验服务器身份。" : "登录已过期，可继续离线记录；请重新登录原账号再上传。";
                }
            }
        });
    }
    protected override void OnDisappearing() { active = false; base.OnDisappearing(); }
    private void Refresh()
    {
        var context = queue.Context;
        scope.Text = context is null ? "尚未确认站点" : $"账号：{context.Username} · 站点：{context.SiteId}\n批次：{queue.BatchNumber}";
        count.Text = $"待上传 {queue.Pending.Count} 件（仅服务器确认后移出）";
        list.ItemsSource = queue.Pending.Reverse().Take(100).Select(s => $"{s.Carrier} / {s.TrackingNumber}\n{s.ScannedAt.ToLocalTime():MM-dd HH:mm:ss}").ToArray();
        loginButton.IsEnabled = logoutButton.IsEnabled = !busy;
        barcode.IsEnabled = carrier.IsEnabled = recordButton.IsEnabled = !busy && session is not null && session.Context.SiteId == context?.SiteId;
        uploadButton.IsEnabled = !busy && session is not null && queue.Pending.Count > 0;
        batchButton.IsEnabled = !busy && queue.Pending.Count == 0 && session is not null;
    }
    private async Task Run(Func<Task> action)
    {
        if (busy) return; busy = true; Refresh();
        try { await action(); }
        catch (Exception ex) { status.Text = ex is HttpRequestException or OperationCanceledException ? "无法确认上传，请检查网络；记录已保留，可重试。" : ex.Message; }
        finally { busy = false; password.Text = ""; Refresh(); if (active && session is not null) barcode.Focus(); }
    }
    private async Task Record() => await Run(() =>
    {
        if (session is null) throw new InvalidOperationException("请先登录。");
        if (session.Context.SiteId != queue.Context?.SiteId) throw new InvalidOperationException("站点已更改，请先处理原站点待上传记录。");
        queue.Add(carrier.SelectedIndex == 0 ? "fedex" : "usps", barcode.Text ?? "", DateTimeOffset.UtcNow);
        barcode.Text = ""; status.Text = "已保存到本机，等待上传确认。"; return Task.CompletedTask;
    });
    private async Task Upload() => await Run(async () =>
    {
        if (session is null) throw new InvalidOperationException("请先登录。");
        // Revalidate online before each upload; stale tokens and changed scopes never reassign queued data.
        var current = await api.Context(session.Token, CancellationToken.None);
        int uploaded = await queue.Upload(current, (batch, ct) => api.Upload(session.Token, batch, ct), CancellationToken.None);
        if (queue.Pending.Count == 0) { queue.Configure(current); session = session with { Context = current }; }
        status.Text = $"服务器已确认 {uploaded} 条。";
    });
}
