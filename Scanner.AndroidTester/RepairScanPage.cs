using System.Net.Http.Json;
using System.Text.Json;
using Scanner.AndroidTester.Services;
using Scanner.Server.Model;

namespace Scanner.AndroidTester;

public sealed class RepairScanPage : ContentPage
{
    private readonly Entry sn = new() { Placeholder = "SN", ReturnType = ReturnType.Next };
    private readonly Entry sku = new() { Placeholder = "SKU", ReturnType = ReturnType.Done };
    private readonly Button upload = new() { Text = UiText.T("上传"), HeightRequest = 64 };
    private readonly Button clearSn = new() { Text = "清除 SN" };
    private readonly Button clearSku = new() { Text = "清除 SKU" };
    private readonly Label message = new() { TextColor = Colors.White };
    private readonly AppSettings settings;
    private static readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(60) };
    private readonly string draftPath = Path.Combine(FileSystem.AppDataDirectory, "repair-scan.json");
    private DateTimeOffset? started;
    private RepairScan? pending;
    private bool busy;

    public RepairScanPage(AppSettings settings)
    {
        this.settings = settings;
        Title = "扫描记录";
        BackgroundColor = Color.FromArgb("#080A0C");
        sn.TextChanged += (_, _) => Changed();
        sku.TextChanged += (_, _) => Changed();
        sn.Completed += (_, _) => { Changed(); sku.Focus(); };
        sku.Completed += (_, _) => { Changed(); upload.Focus(); };
        clearSn.Clicked += (_, _) => { sn.Text = ""; sn.Focus(); };
        clearSku.Clicked += (_, _) => { sku.Text = ""; sku.Focus(); };
        upload.Clicked += async (_, _) => await UploadAsync();
        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Padding = 24, Spacing = 16,
            Children = { new Label { Text = "开始维修 · SN / SKU 各 1–50 位", TextColor = Colors.White },
                new Label { Text = "SN", TextColor = Colors.White }, sn, clearSn,
                new Label { Text = "SKU", TextColor = Colors.White }, sku, clearSku, upload, message }
        } };
        if (File.Exists(draftPath))
        {
            try
            {
                pending = JsonSerializer.Deserialize<RepairScan>(File.ReadAllText(draftPath));
                if (pending is not null) { started = pending.StartedAt; sn.Text = pending.Sn; sku.Text = pending.Sku; }
            }
            catch (Exception) { message.Text = "未能读取待上传记录，请检查本机存储。"; busy = true; }
        }
        Changed();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(200), () => { if (pending is null) sn.Focus(); });
    }

    private static bool Valid(string? code) => code?.Trim() is { Length: >= 1 and <= 50 } value && !value.Any(char.IsControl);

    private void Changed()
    {
        if (started is null && (!string.IsNullOrWhiteSpace(sn.Text) || !string.IsNullOrWhiteSpace(sku.Text)))
            started = DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(sn.Text) && string.IsNullOrWhiteSpace(sku.Text)) started = null;
        sn.IsEnabled = sku.IsEnabled = clearSn.IsEnabled = clearSku.IsEnabled = !busy && pending is null;
        upload.IsEnabled = !busy && Valid(sn.Text) && Valid(sku.Text);
        if (!busy && pending is null)
            message.Text = started is null ? "请扫描 SN 和 SKU。" : $"开始维修：{started.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss}；两项各不超过 50 位才可上传。";
    }

    private async Task UploadAsync()
    {
        if (busy || !Valid(sn.Text) || !Valid(sku.Text) || started is null) return;
        busy = true;
        Changed();
        try
        {
            var record = pending ?? new RepairScan(Guid.NewGuid(), sn.Text.Trim(), sku.Text.Trim(), started.Value);
            // Persist before sending so an ambiguous response can always be retried with the same ID.
            if (pending is null)
            {
                File.WriteAllText(draftPath + ".tmp", JsonSerializer.Serialize(record));
                File.Move(draftPath + ".tmp", draftPath, true);
                pending = record;
            }
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(settings.UploadBaseUrl, "api/repair-scans"))
                { Content = JsonContent.Create(record) };
            request.Headers.Add("X-Upload-Key", settings.UploadApiKey.Trim());
            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"上传失败（HTTP {(int)response.StatusCode}），记录已保留，请重试。");
            using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!result.RootElement.TryGetProperty("scanId", out var id) || id.GetGuid() != record.ScanId)
                throw new InvalidOperationException("服务器未确认本条记录，请重试。");
            File.Delete(draftPath);
            pending = null;
            sn.Text = sku.Text = "";
            started = null;
            message.Text = "上传成功：开始维修。";
        }
        catch (Exception ex) { message.Text = "上传未完成，已保留记录。" + ex.Message; }
        finally
        {
            busy = false;
            var status = message.Text;
            Changed();
            message.Text = status;
            if (pending is null) sn.Focus();
        }
    }
}
