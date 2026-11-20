using System.Text.Json;
using Scanner.ShippingCowAndroid.Services;

namespace Scanner.ShippingCowAndroid;

public sealed class CarrierReceivingPage : ContentPage
{
    private const string SessionKey = "carrier-receiving-session-v1";
    private readonly Entry barcode = new() { Placeholder = UiText.T("扫描运单号"), ReturnType = ReturnType.Done, FontSize = 21 };
    private readonly Label status = new() { FontSize = 14 };
    private readonly Button retryButton = new() { Text = UiText.T("重试"), IsVisible = false };
    private readonly Button uploadButton = new() { Text = UiText.T("上传"), HeightRequest = 58 };
    private readonly Button recordButton = new() { Text = UiText.T("记录"), HeightRequest = 52 };
    private readonly CarrierReceiptHistoryView history;
    private readonly CarrierReceivingQueue queue;
    private readonly ConfiguredReceivingConnection connection;
    private readonly AppSettings settings;
    private bool busy, active;

    public CarrierReceivingPage(CarrierReceivingQueue queue, CarrierReceivingApi api, AppSettings settings)
    {
        this.queue = queue; this.settings = settings; connection = new(api, settings);
        queue.PrepareCapture(settings.UploadBaseUrl.AbsoluteUri, settings.ReceivingUsername);
        history = new(queue); history.Changed += Refresh;
        Title = UiText.T("包裹收货");
        retryButton.Clicked += async (_, _) => await Run(Connect, connecting: true);
        recordButton.Clicked += async (_, _) => await Record(); barcode.Completed += async (_, _) => await Record();
        uploadButton.Clicked += async (_, _) => await Upload();
        var top = new VerticalStackLayout { Spacing = 10, Children = { barcode, recordButton, status, retryButton } };
        var grid = new Grid { Padding = 16, RowSpacing = 10, RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) } };
        grid.Add(top, 0, 0); grid.Add(history, 0, 1); grid.Add(uploadButton, 0, 2); Content = grid;
        Refresh();
    }
    private sealed record SavedLogin(string Server, ReceivingLogin Login);
    private bool CanRecord => connection.Session is { } login && queue.Context is { } context
        && login.Context.UserId == context.UserId && login.Context.DomainId == context.DomainId && login.Context.Server == context.Server;

    protected override async void OnAppearing()
    {
        base.OnAppearing(); active = true;
        await Run(async () =>
        {
            string? saved = null;
            try { saved = await SecureStorage.Default.GetAsync(SessionKey); } catch (Exception) { }
            if (saved is not null)
            {
                SavedLogin? stored = null;
                try { stored = JsonSerializer.Deserialize<SavedLogin>(saved); } catch (JsonException) { }
                if (stored?.Server == settings.UploadBaseUrl.AbsoluteUri && stored.Login is not null && connection.Restore(stored.Login))
                    queue.Configure(stored.Login.Context);
            }
            await Connect();
        }, connecting: true);
    }
    protected override void OnDisappearing() { active = false; base.OnDisappearing(); }
    private async Task SaveSession()
    {
        try
        {
            if (connection.Session is not { } session) { SecureStorage.Default.Remove(SessionKey); return; }
            await SecureStorage.Default.SetAsync(SessionKey, JsonSerializer.Serialize(new SavedLogin(settings.UploadBaseUrl.AbsoluteUri, session)));
        }
        catch (Exception) { }
    }
    private async Task Connect()
    {
        status.Text = UiText.T("连接中…");
        var session = await connection.Connect(CancellationToken.None);
        queue.Configure(session.Context);
        await SaveSession(); status.Text = UiText.T("可扫码。");
    }
    private void Refresh()
    {
        history.Refresh();
        retryButton.IsVisible = !busy && !CanRecord; retryButton.IsEnabled = !busy;
        uploadButton.IsEnabled = !busy && queue.Pending.Count > 0;
    }
    private async Task Run(Func<Task> action, bool connecting = false)
    {
        if (busy) return; busy = true; Refresh();
        try { await action(); }
        catch (Exception ex)
        {
            var httpCode = ex is CarrierReceivingApiException apiError ? (int?)apiError.StatusCode : null;
            System.Diagnostics.Debug.WriteLine("Receipt action failed: " + ex.GetType().Name + (httpCode is null ? "" : " HTTP " + httpCode));
            status.Text = httpCode is int code
                ? UiText.ReceivingHttpError(code, ex.Message)
                : connecting && ex is (HttpRequestException or TaskCanceledException)
                    ? UiText.T("离线，可继续扫码。")
                    : UiText.Error(ex);
            if (connection.Session is null) await SaveSession();
        }
        finally { busy = false; Refresh(); if (active) barcode.Focus(); }
    }
    private async Task Record()
    {
        bool alarm = false;
        try
        {
            string? warning = queue.Add(barcode.Text ?? "", DateTimeOffset.UtcNow);
            barcode.Text = ""; status.Text = warning is null ? UiText.T("已记录。") : UiText.TranslateError(warning); alarm = warning is not null;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { status.Text = UiText.Error(ex); }
        catch (Exception) { status.Text = UiText.T("保存失败，请重试。"); }
        Refresh(); if (active) barcode.Focus();
        if (alarm) await NativeScanSound.PlayAsync(false, true);
    }
    private async Task Upload() => await Run(async () =>
    {
        status.Text = UiText.T("上传中…");
        var session = await connection.PrepareUpload(CancellationToken.None);
        queue.Configure(session.Context);
        await queue.Upload(session.Context, connection.Upload, CancellationToken.None);
        status.Text = UiText.T("上传成功。");
    });
}
