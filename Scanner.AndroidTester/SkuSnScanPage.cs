using Scanner.AndroidTester.Services;
namespace Scanner.AndroidTester;
public sealed class SkuSnScanPage : ContentPage
{
    private readonly SkuSnSession _session;
    private readonly Entry _input = new() { ReturnType = ReturnType.Done, FontSize = 22 };
    private readonly Label _status = new();
    private readonly Label _batch = new();
    private readonly CollectionView _list = new();
    private readonly Button _upload = new() { Text = "上传批次" };
    private readonly HttpClient _client;
    private readonly AppSettings _settings;
    private bool _active;
    public SkuSnScanPage(SkuSnSession session, AppSettings settings)
    {
        _session = session; _settings = settings; _client = new() { BaseAddress = settings.UploadBaseUrl, Timeout = TimeSpan.FromSeconds(60) }; Title = "SKU / SN 扫描";
        _input.Completed += (_, _) => Scan();
        var record = new Button { Text = "记录" }; record.Clicked += (_, _) => Scan();
        var reset = new Button { Text = "重扫 SKU" }; reset.Clicked += (_, _) => { session.ResetPair(); Refresh(); _input.Focus(); };
        _upload.Clicked += async (_, _) => {
            _upload.IsEnabled = false;
            try { await session.UploadAsync(_client, settings); _status.Text = "上传成功"; }
            catch (Exception ex) { _status.Text = ex.Message; }
            finally { Refresh(); if (_active) _input.Focus(); }
        };
        _list.ItemTemplate = new DataTemplate(() => { var label = new Label { Padding = 8 }; label.SetBinding(Label.TextProperty, "."); return label; });
        var done = new Button { Text = "完成" }; done.Clicked += async (_, _) => await Navigation.PopAsync();
        var grid = new Grid { Padding = 16, RowSpacing = 8, RowDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) } };
        grid.Add(new VerticalStackLayout { Spacing = 8, Children = { _batch, _input, record, reset, _status } },0,0);
        grid.Add(_list,0,1); grid.Add(new VerticalStackLayout { Spacing = 8, Children = { _upload, done } },0,2); Content = grid; Refresh();
    }
    private void Scan()
    {
        try { _session.Scan(_input.Text ?? ""); _status.Text = _session.PendingSku is null ? "已记录" : "扫描 SN"; }
        catch (Exception ex) { _status.Text = ex.Message; }
        _input.Text = ""; Refresh(); _input.Focus();
    }
    private void Refresh()
    {
        _batch.Text = $"{_session.BatchNumber} · {_session.Items.Count} 条";
        _input.Placeholder = _session.PendingSku is null ? "SKU" : $"SN · {_session.PendingSku}";
        _list.ItemsSource = _session.Items.Reverse().Select(x => $"{x.Sku} · {x.Sn}").ToArray();
        _upload.IsEnabled = !_session.IsUploading && _session.PendingSku is null && _session.Items.Count > 0;
    }
    protected override void OnAppearing() { base.OnAppearing(); _active = true; Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(200), () => { if (_active) _input.Focus(); }); }
    protected override void OnDisappearing() { _active = false; base.OnDisappearing(); }
}
