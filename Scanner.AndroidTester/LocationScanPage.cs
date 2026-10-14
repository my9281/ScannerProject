using Scanner.AndroidTester.Services;
namespace Scanner.AndroidTester;
public sealed class LocationScanPage : ContentPage
{
    private readonly LocationScanSession _session;
    private readonly LocationScanApiService _api;
    private readonly Entry _input = new() { Placeholder = UiText.T("库位 ID"), ReturnType = ReturnType.Done, FontSize = 22 };
    private readonly Label _status = new() { Text = "" };
    private readonly Label _count = new();
    private readonly CollectionView _list = new() { SelectionMode = SelectionMode.None };
    private readonly Button _upload = new() { Text = UiText.T("上传") };
    private bool _active;
    public LocationScanPage(LocationScanSession session, LocationScanApiService api)
    {
        _session = session; _api = api; Title = UiText.T("库位扫描"); BackgroundColor = Color.FromArgb("#080A0C");
        var header = new VerticalStackLayout { Spacing = 8 };
        header.Add(_input);
        var record = new Button { Text = UiText.T("记录库位") };
        record.Clicked += (_, _) => Scan(); _input.Completed += (_, _) => Scan(); header.Add(record);
        header.Add(_status); header.Add(_count);
        _list.ItemTemplate = new DataTemplate(() => { var label = new Label { Padding = 8 }; label.SetBinding(Label.TextProperty, "."); return label; });
        _upload.Clicked += async (_, _) => await Upload();
        var done = new Button { Text = UiText.T("完成") }; done.Clicked += async (_, _) => await Navigation.PopAsync();
        var footer = new VerticalStackLayout { Spacing = 8, Children = { _upload, done } };
        var grid = new Grid { Padding = 16, RowSpacing = 12, RowDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) } };
        grid.Add(header, 0, 0); grid.Add(_list, 0, 1); grid.Add(footer, 0, 2); Content = grid; Refresh();
    }
    protected override void OnAppearing() { base.OnAppearing(); _active = true; Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(200), () => { if (_active) _input.Focus(); }); }
    protected override void OnDisappearing() { _active = false; base.OnDisappearing(); }
    private void Scan()
    {
        try { _session.Add(_input.Text ?? "", DateTimeOffset.Now); _status.Text = UiText.T("已记录库位。"); }
        catch (ArgumentException ex) { _status.Text = UiText.Error(ex); }
        _input.Text = ""; Refresh(); _input.Focus();
    }
    private void Refresh()
    {
        _count.Text = UiText.F("待上传 {0} 个库位", _session.Pending.Count);
        _list.ItemsSource = _session.Pending.Reverse().Select(x => $"{x.LocationId}\n{x.ScannedAt:yyyy-MM-dd HH:mm:ss}").ToArray();
        _upload.IsEnabled = !_session.IsUploading && _session.Pending.Count > 0;
    }
    private async Task Upload()
    {
        _upload.IsEnabled = false; _status.Text = UiText.T("正在上传…");
        try { var count = await _session.UploadAsync(_api); _status.Text = UiText.F("已确认 {0} 个库位。", count); }
        catch (TaskCanceledException) { _status.Text = UiText.T("上传超时，未确认记录已保留。"); }
        catch (HttpRequestException) { _status.Text = UiText.T("无法连接服务器，未确认记录已保留。"); }
        catch (Exception ex) { _status.Text = UiText.T("上传失败：") + UiText.Error(ex); }
        finally { Refresh(); if (_active) _input.Focus(); }
    }
}
