using Scanner.BreezaryStar.AndroidScanner.Services;

namespace Scanner.BreezaryStar.AndroidScanner;

public sealed class MainPage : ContentPage
{
    private readonly InboundCache cache;
    private readonly InboundUploadClient uploader;
    private readonly Picker location = new() { Title = "选择缓存位置（1–100）", TextColor = Colors.White, TitleColor = Colors.Gray };
    private readonly Entry input = new() { Placeholder = "扫描条码，扫码枪以回车结束", FontSize = 22, ReturnType = ReturnType.Done, IsEnabled = false };
    private readonly Button start = new() { Text = "开始扫描" };
    private readonly Button record = new() { Text = "记录当前条码", IsEnabled = false };
    private readonly Button upload = new() { Text = "上传全部待上传记录", IsEnabled = false };
    private readonly Label status = new() { Text = "选择位置后开始扫描，记录会先保存在本机。", TextColor = Color.FromArgb("#D9B866") };
    private readonly Label count = new();
    private readonly CollectionView list = new() { SelectionMode = SelectionMode.None };
    private readonly Entry server = new() { Placeholder = "服务器 HTTPS 地址" };
    private readonly Entry key = new() { Placeholder = "上传密钥（对应 Web Upload:ApiKey）", IsPassword = true };
    private readonly VerticalStackLayout settings = new() { IsVisible = false, Spacing = 8 };
    private bool active, ready, uploading;
    private int saving;

    public MainPage(InboundCache cache, InboundUploadClient uploader)
    {
        this.cache = cache; this.uploader = uploader;
        Title = "扫码入库";
        location.ItemsSource = Enumerable.Range(1, 100).ToArray();
        var savedLocation = Preferences.Get("inbound.location", 0);
        if (savedLocation is >= 1 and <= 100) location.SelectedIndex = savedLocation - 1;
        server.Text = Preferences.Get("inbound.server", "https://wms.ymforever.com/");
        var connection = new Button { Text = "连接设置" };
        connection.Clicked += (_, _) => settings.IsVisible = !settings.IsVisible;
        var saveSettings = new Button { Text = "保存连接设置" };
        saveSettings.Clicked += async (_, _) =>
        {
            try
            {
                if (uploading) return;
                Preferences.Set("inbound.server", InboundUploadClient.Server(server.Text ?? "").ToString());
                await SecureStorage.Default.SetAsync("inbound.upload-key", key.Text?.Trim() ?? "");
                settings.IsVisible = false; status.Text = "连接设置已保存。";
            }
            catch (Exception ex) { status.Text = "保存设置失败：" + ex.Message; }
        };
        settings.Add(server); settings.Add(key); settings.Add(saveSettings);
        start.Clicked += (_, _) =>
        {
            if (!ready || uploading || saving > 0) return;
            if (!active && location.SelectedIndex < 0) { status.Text = "请先选择 1–100 的缓存位置。"; return; }
            active = !active;
            if (active)
            {
                Preferences.Set("inbound.location", location.SelectedIndex + 1);
                status.Text = $"正在扫描 · 缓存位置 {location.SelectedIndex + 1}";
            }
            else status.Text = "扫描已停止，可更换位置或上传。";
            Controls(); if (active) input.Focus();
        };
        input.Completed += async (_, _) => await RecordAsync();
        record.Clicked += async (_, _) => await RecordAsync();
        upload.Clicked += async (_, _) => await UploadAsync();
        list.ItemTemplate = new DataTemplate(() =>
        {
            var text = new Label { Padding = new Thickness(8, 10), LineBreakMode = LineBreakMode.WordWrap };
            text.SetBinding(Label.TextProperty, nameof(CachedInboundScan.Display));
            return text;
        });
        var header = new VerticalStackLayout { Spacing = 10, Children = { location, start, input, record, status, count } };
        var footer = new VerticalStackLayout { Spacing = 8, Children = { upload, connection, settings } };
        var grid = new Grid { Padding = 16, RowSpacing = 10, RowDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) } };
        grid.Add(header, 0, 0); grid.Add(list, 0, 1); grid.Add(footer, 0, 2); Content = grid;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await RefreshAsync(); ready = true;
            try { key.Text = await SecureStorage.Default.GetAsync("inbound.upload-key") ?? ""; }
            catch { status.Text = "无法读取保存的密钥，请在连接设置中重新填写；本地扫描仍可使用。"; }
        }
        catch (Exception ex) { ready = false; status.Text = "本地缓存读取失败，已停止扫描以保护数据：" + ex.Message; }
        Controls();
    }

    private void Controls()
    {
        location.IsEnabled = !active && !uploading && saving == 0;
        start.Text = active ? "停止扫描" : "开始扫描";
        start.IsEnabled = ready && !uploading && saving == 0;
        input.IsEnabled = record.IsEnabled = ready && active && !uploading;
        upload.IsEnabled = ready && !active && !uploading && saving == 0;
        server.IsEnabled = key.IsEnabled = !uploading;
    }

    private async Task RecordAsync()
    {
        if (!active || uploading || !ready) return;
        var barcode = input.Text ?? "";
        if (string.IsNullOrWhiteSpace(barcode)) return;
        var selected = location.SelectedIndex + 1;
        input.Text = ""; saving++; Controls();
        try
        {
            await cache.AddAsync(selected, barcode);
            status.Text = $"已保存到本机 · 位置 {selected} · {barcode}";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            active = false;
            if (string.IsNullOrEmpty(input.Text)) input.Text = barcode;
            status.Text = "保存失败，此条未确认入库，请重试：" + ex.Message;
        }
        finally { saving--; Controls(); if (active) input.Focus(); }
    }

    private async Task RefreshAsync()
    {
        var all = await cache.SnapshotAsync();
        var pending = all.Count(x => x.UploadedAt is null);
        count.Text = $"本机共 {all.Count} 条 · 待上传 {pending} 条 · 已上传 {all.Count - pending} 条（显示最近 200 条）";
        list.ItemsSource = all.Take(200).ToArray();
    }

    private async Task UploadAsync()
    {
        if (active || uploading || saving > 0 || !ready) return;
        uploading = true; Controls(); status.Text = "正在上传全部位置的待上传记录…";
        try
        {
            var uploaded = await uploader.UploadPendingAsync(cache, server.Text ?? "", key.Text ?? "");
            status.Text = uploaded == 0 ? "没有待上传记录。" : $"服务器已确认 {uploaded} 条，历史记录仍保留在本机。";
        }
        catch (Exception ex) { status.Text = "上传未完成，未确认记录保留在本机，可重试：" + ex.Message; }
        finally
        {
            uploading = false;
            try { await RefreshAsync(); } catch (Exception ex) { status.Text = "本地读取失败：" + ex.Message; ready = false; }
            Controls();
        }
    }
}
