using System.Net.Http.Json;
using System.Text.Json;
using Scanner.AndroidTester.Services;
using Scanner.Server.Model;
namespace Scanner.AndroidTester;

public sealed class LocationSkuPage : ContentPage
{
    private readonly HttpClient _client;
    private readonly bool _bind;
    private readonly Entry _location = new() { Placeholder = UiText.T("扫描库位"), ReturnType = ReturnType.Next };
    private readonly Entry _sku = new() { Placeholder = UiText.T("扫描 SKU"), ReturnType = ReturnType.Done };
    private readonly Entry _actor = new() { Placeholder = UiText.T("操作人（必填）") };
    private readonly Entry _quantity = new() { Text = "1", Keyboard = Keyboard.Numeric, Placeholder = UiText.T("数量") };
    private readonly Label _rows = new();
    private readonly Label _status = new();
    private readonly VerticalStackLayout _form;
    private readonly Button _retry = new() { Text = UiText.T("重试未确认操作"), IsVisible = false };
    private string? _loaded;
    private string? _selected;
    private LocationSkuRow[] _items = [];
    private LocationSkuChange? _pending;
    private bool _busy;
    private string PendingKey => _bind ? "LocationSkuPendingBind" : "LocationSkuPendingAdjust";

    public LocationSkuPage(AppSettings settings, bool bind)
    {
        _bind = bind; Title = bind ? UiText.T("库位核对绑定") : UiText.T("上架配件");
        _client = new HttpClient { BaseAddress = settings.UploadBaseUrl, Timeout = TimeSpan.FromSeconds(30) };
        _client.DefaultRequestHeaders.Add("X-Upload-Key", settings.UploadApiKey);
        _actor.Text = Preferences.Get("LocationSkuOperator", "");
        _form = new VerticalStackLayout { Padding = 16, Spacing = 12 };
        _form.Add(_actor); _form.Add(_location);
        var load = new Button { Text = UiText.T("查询库位") }; load.Clicked += async (_, _) => await Load(); _form.Add(load);
        _form.Add(_rows); _form.Add(_sku);
        _location.TextChanged += (_, _) => { _loaded = null; _selected = null; _items = []; _rows.Text = ""; };
        _sku.TextChanged += (_, _) => _selected = null;
        _location.Completed += async (_, _) => await Load();
        _sku.Completed += async (_, _) => await ScanSku();
        var verify = new Button { Text = bind ? UiText.T("核对并绑定") : UiText.T("核对 SKU") };
        verify.Clicked += async (_, _) => await ScanSku(); _form.Add(verify);
        if (!bind)
        {
            _form.Add(_quantity);
            var plus = new Button { Text = UiText.T("↑ 增加数量") }; var minus = new Button { Text = UiText.T("↓ 减少数量") };
            plus.Clicked += async (_, _) => await Adjust(1); minus.Clicked += async (_, _) => await Adjust(-1);
            _form.Add(plus); _form.Add(minus);
        }
        _retry.Clicked += async (_, _) => await SendPending();
        var saved = Preferences.Get(PendingKey, "");
        if (!string.IsNullOrEmpty(saved))
        {
            _pending = JsonSerializer.Deserialize<LocationSkuChange>(saved);
            _form.IsEnabled = _pending is null; _retry.IsVisible = _pending is not null;
            if (_pending is not null) _status.Text = UiText.F("库位 {0} / {1} 有未确认操作，请重试。", _pending.LocationId, _pending.Sku);
        }
        Content = new ScrollView { Content = new VerticalStackLayout { Children = { _form, _retry, _status } } };
    }
    protected override void OnAppearing() { base.OnAppearing(); _location.Focus(); }
    private async Task Load()
    {
        if (_busy || _pending is not null) return;
        _busy = true; _form.IsEnabled = false; _loaded = null; _selected = null;
        try
        {
            var id = _location.Text?.Trim() ?? "";
            using var response = await _client.GetAsync("api/warehouse-locations/skus?locationId=" + Uri.EscapeDataString(id));
            await Ensure(response);
            _items = await response.Content.ReadFromJsonAsync<LocationSkuRow[]>() ?? [];
            _loaded = id; ShowRows(); _status.Text = UiText.T("请扫描 SKU"); _sku.Text = ""; _sku.Focus();
        }
        catch (Exception ex) { _rows.Text = ""; _status.Text = UiText.Error(ex); }
        finally { _busy = false; _form.IsEnabled = true; if (_loaded is not null) _sku.Focus(); }
    }
    private void ShowRows() => _rows.Text = _items.Length == 0 ? UiText.T("此库位暂无绑定 SKU") : string.Join("\n", _items.Select(x => UiText.F("{0}    数量：{1}", x.Sku, x.Quantity)));
    private async Task ScanSku()
    {
        if (_busy || _pending is not null) return;
        if (_loaded is null || string.IsNullOrWhiteSpace(_sku.Text)) { _status.Text = UiText.T("请先查询库位，再扫描 SKU。"); return; }
        var sku = _sku.Text.Trim();
        if (_bind) await Change(sku, 0);
        else if (_items.Any(x => x.Sku == sku)) { _selected = sku; _status.Text = UiText.F("已核对 {0}，可增减数量。", sku); }
        else { _selected = null; _status.Text = UiText.T("SKU 与库位绑定信息不符。"); }
    }
    private async Task Adjust(int sign)
    {
        if (_busy || _pending is not null) return;
        if (_selected is null || !int.TryParse(_quantity.Text, out var count) || count <= 0) { _status.Text = UiText.T("请核对 SKU 并输入正整数数量。"); return; }
        await Change(_selected, sign * count);
    }
    private async Task Change(string sku, int delta)
    {
        if (string.IsNullOrWhiteSpace(_actor.Text)) { _status.Text = UiText.T("请填写操作人。"); return; }
        Preferences.Set("LocationSkuOperator", _actor.Text.Trim());
        _pending = new(_loaded!, sku, delta, Guid.NewGuid(), _actor.Text.Trim());
        Preferences.Set(PendingKey, JsonSerializer.Serialize(_pending));
        await SendPending();
    }
    private async Task SendPending()
    {
        if (_busy || _pending is null) return;
        _busy = true; _form.IsEnabled = false; _retry.IsEnabled = false;
        try
        {
            using var response = await _client.PostAsJsonAsync("api/warehouse-locations/skus/" + (_bind ? "bind" : "adjust"), _pending);
            if ((int)response.StatusCode is >= 400 and < 500) { _pending = null; }
            await Ensure(response);
            var result = await response.Content.ReadFromJsonAsync<LocationSkuRow>() ?? throw new InvalidOperationException(UiText.T("服务器未确认，请重试。"));
            _items = _items.Where(x => x.Sku != result.Sku).Append(result).OrderBy(x => x.Sku).ToArray();
            _pending = null; ShowRows(); _status.Text = UiText.T("已保存并记录日志。");
            _sku.Text = ""; _selected = null;
        }
        catch (Exception ex) { _status.Text = UiText.Error(ex) + (_pending is null ? "" : UiText.T(" 操作结果未确认，请点重试；不要重复提交。")); }
        finally { if (_pending is null) Preferences.Remove(PendingKey); _busy = false; _retry.IsVisible = _pending is not null; _retry.IsEnabled = true; _form.IsEnabled = _pending is null; if (_pending is null) _sku.Focus(); }
    }
    private static async Task Ensure(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var message = UiText.F("请求失败（{0}）", (int)response.StatusCode);
        try { using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); if (json.RootElement.TryGetProperty("message", out var m)) message = m.GetString() ?? message; } catch (JsonException) { }
        throw new InvalidOperationException(message);
    }
    protected override bool OnBackButtonPressed() => _pending is not null || _busy || base.OnBackButtonPressed();
}
