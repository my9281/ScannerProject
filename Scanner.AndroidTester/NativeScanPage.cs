using Scanner.AndroidTester.Services;

namespace Scanner.AndroidTester;

public enum NativeScanMode { PreScan, Detection, Pallet }

public sealed class NativeScanPage : ContentPage
{
    private readonly NativeScanMode _mode;
    private readonly ScanSession _scans;
    private readonly PalletScanSession _pallets;
    private readonly PalletScanApiService _api;
    private readonly Entry _input = new() { Placeholder = "SN", ReturnType = ReturnType.Done, FontSize = 22, TextColor = Color.FromArgb("#F2F2F2"), BackgroundColor = Color.FromArgb("#181C20") };
    private readonly Label _status = new() { Text = "", TextColor = Color.FromArgb("#92979D") };
    private readonly Label _count = new() { TextColor = Color.FromArgb("#F2F2F2") };
    private readonly CollectionView _list = new() { SelectionMode = SelectionMode.None };
    private readonly Button _upload = new() { Text = "上传" };
    private readonly Entry _number = new() { Keyboard = Keyboard.Numeric, WidthRequest = 90, HorizontalTextAlignment = TextAlignment.Center, FontSize = 24, TextColor = Color.FromArgb("#F2F2F2") };
    private bool _active;

    public NativeScanPage(NativeScanMode mode, ScanSession scans, PalletScanSession pallets, PalletScanApiService api)
    {
        _mode = mode; _scans = scans; _pallets = pallets; _api = api;
        Title = mode switch { NativeScanMode.PreScan => "预扫描", NativeScanMode.Detection => "检测扫描", _ => "良品区扫描" };
        BackgroundColor = Color.FromArgb("#080A0C");
        var header = new VerticalStackLayout { Spacing = 10 };
        if (mode == NativeScanMode.Pallet)
        {
            _number.Text = pallets.PalletNumber.ToString();
            var up = new Button { Text = "▲", HeightRequest = 44 };
            var down = new Button { Text = "▼", HeightRequest = 44 };
            up.Clicked += (_, _) => ChangePallet(1);
            down.Clicked += (_, _) => ChangePallet(-1);
            header.Add(new Label { Text = "托盘号（1–100）", TextColor = Color.FromArgb("#F2F2F2") });
            header.Add(new HorizontalStackLayout { Spacing = 4, Children = { _number, new VerticalStackLayout { Children = { up, down } } } });
        }
        header.Add(_input);
        var record = new Button { Text = mode == NativeScanMode.Detection ? "检测" : "记录" };
        record.Clicked += async (_, _) => await ScanAsync();
        _input.Completed += async (_, _) => await ScanAsync();
        header.Add(record);
        header.Add(_status);
        header.Add(_count);
        _list.ItemTemplate = new DataTemplate(() =>
        {
            var label = new Label { Padding = new Thickness(4, 8), TextColor = Color.FromArgb("#F2F2F2"), LineBreakMode = LineBreakMode.WordWrap };
            label.SetBinding(Label.TextProperty, ".");
            return label;
        });
        var footer = new VerticalStackLayout { Spacing = 8 };
        if (mode == NativeScanMode.Pallet)
        {
            _upload.Clicked += async (_, _) => await UploadAsync();
            footer.Add(_upload);
        }
        var done = new Button { Text = "完成" };
        done.Clicked += async (_, _) => await Navigation.PopAsync();
        footer.Add(done);
        var grid = new Grid { Padding = 16, RowSpacing = 12, RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) } };
        grid.Add(header, 0, 0); grid.Add(_list, 0, 1); grid.Add(footer, 0, 2);
        Content = grid;
        Refresh();
    }
    protected override void OnAppearing() { base.OnAppearing(); _active = true; Refresh(); Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(200), () => { if (_active) _input.Focus(); }); }
    protected override void OnDisappearing() { _active = false; base.OnDisappearing(); }
    private void ChangePallet(int delta)
    {
        int current = int.TryParse(_number.Text, out var parsed) ? parsed : _pallets.PalletNumber;
        _pallets.PalletNumber = Math.Clamp(current + delta, 1, 100);
        _number.Text = _pallets.PalletNumber.ToString();
        _input.Focus();
    }
    private async Task ScanAsync()
    {
        var value = _input.Text ?? "";
        if (string.IsNullOrWhiteSpace(value)) { _status.Text = "请先扫描或输入内容。"; _input.Focus(); return; }
        Handler!.MauiContext!.Services.GetRequiredService<LatestScan>().Record(value);
        bool same = false, hit = false;
        try
        {
            if (_mode == NativeScanMode.PreScan) { _status.Text = _scans.AddPreScan(value) ? "已保存。" : "重复 SN，未重复记录。"; }
            else if (_mode == NativeScanMode.Pallet)
            {
                if (!int.TryParse(_number.Text, out var number) || number is < 1 or > 100) throw new ArgumentException("托盘号必须为 1–100 的整数。");
                _pallets.PalletNumber = number;
                _pallets.Add(value, DateTimeOffset.Now); _status.Text = "已记录。";
            }
            else
            {
                var result = _scans.Detect(value)!;
                same = result.SameAsLast; hit = result.InPreScanList;
                _status.Text = !same && !hit ? "扫描完成，无重复或列表匹配。" : (same ? "相同：与上次扫描一致。 " : "") + (hit ? "报警：命中预扫描列表。" : "");
            }
            _input.Text = "";
            _status.TextColor = same || hit ? Color.FromArgb("#FF7474") : Color.FromArgb("#F2F2F2");
            Refresh();
            _input.Focus();
            if (same || hit) await NativeScanSound.PlayAsync(same, hit);
        }
        catch (Exception ex) { _status.Text = ex.Message; _input.Text = ""; _input.Focus(); }
    }
    private void Refresh()
    {
        _count.Text = _mode switch
        {
            NativeScanMode.PreScan => $"已记录 {_scans.PreScans.Count} 条",
            NativeScanMode.Detection => $"上次扫描：{_scans.LastScan ?? "暂无"}",
            _ => $"待上传 {_pallets.Pending.Count} 条"
        };
        if (_mode == NativeScanMode.PreScan) _list.ItemsSource = _scans.PreScans.Reverse().ToArray();
        if (_mode == NativeScanMode.Pallet) _list.ItemsSource = _pallets.Pending.Reverse().Select(x => $"{x.Sn}\n托盘 {x.PalletNumber} · {x.ScannedAt:yyyy-MM-dd HH:mm:ss}").ToArray();
        _upload.IsEnabled = !_pallets.IsUploading && _pallets.Pending.Count > 0;
    }
    private async Task UploadAsync()
    {
        _upload.IsEnabled = false; _status.Text = "正在上传…";
        try { var count = await _pallets.UploadAsync(_api); _status.Text = $"成功上传 {count} 条。"; }
        catch (TaskCanceledException) { _status.Text = "上传超时，未确认记录已保留，请重试。"; }
        catch (HttpRequestException) { _status.Text = "无法连接服务器，未确认记录已保留。"; }
        catch (Exception ex) { _status.Text = "上传失败：" + ex.Message; }
        finally { Refresh(); if (_active) _input.Focus(); }
    }
}

internal static class NativeScanSound
{
    private static readonly SemaphoreSlim Queue = new(1, 1);
    public static async Task PlayAsync(bool same, bool hit)
    {
        await Queue.WaitAsync();
        try
        {
#if ANDROID
            using var tone = new Android.Media.ToneGenerator(Android.Media.Stream.Music, 100);
            if (same)
                for (int i = 0; i < 2; i++) { tone.StartTone(Android.Media.Tone.Dtmf9, 120); await Task.Delay(190); }
            if (hit)
                for (int i = 0; i < 3; i++)
                {
                    tone.StartTone(Android.Media.Tone.Dtmf1, 220); await Task.Delay(270);
                    tone.StartTone(Android.Media.Tone.Dtmf9, 220); await Task.Delay(270);
                }
#endif
        }
        finally { Queue.Release(); }
    }
}

