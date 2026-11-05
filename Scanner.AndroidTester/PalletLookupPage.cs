using Scanner.AndroidTester.Services;

namespace Scanner.AndroidTester;

public sealed class PalletLookupPage : ContentPage
{
    private readonly PalletScanApiService _api;
    private readonly AppSettings _settings;
    private readonly Entry _input = new() { Placeholder = UiText.T("扫描 SN 查询托盘"), ReturnType = ReturnType.Search, FontSize = 22 };
    private readonly Label _pallet = new() { FontSize = 64, FontAttributes = FontAttributes.Bold, TextColor = Colors.White };
    private readonly Label _details = new() { FontSize = 18, TextColor = Colors.White };
    private readonly Label _status = new() { Text = UiText.T("扫描 SN 查询托盘"), TextColor = Colors.White };
    private readonly Button _query = new() { Text = UiText.T("查询"), HeightRequest = 60 };
    private CancellationTokenSource? _lifetime;
    private bool _busy;

    public PalletLookupPage(PalletScanApiService api, AppSettings settings)
    {
        _api = api;
        _settings = settings;
        Title = UiText.T("托盘查询");
        BackgroundColor = Color.FromArgb("#080A0C");
        _input.Completed += async (_, _) => await LookupAsync();
        _query.Clicked += async (_, _) => await LookupAsync();
        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Padding = 24, Spacing = 20,
            Children = { _input, _query, _status, _pallet, _details }
        } };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _lifetime = new CancellationTokenSource();
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(200), () =>
        {
            if (_lifetime?.IsCancellationRequested == false) _input.Focus();
        });
    }

    protected override void OnDisappearing()
    {
        _lifetime?.Cancel();
        _lifetime?.Dispose();
        _lifetime = null;
        base.OnDisappearing();
    }

    private async Task LookupAsync()
    {
        if (_busy || _lifetime is null) return;
        var token = _lifetime.Token;
        string sn = (_input.Text ?? string.Empty).Trim();
        _pallet.Text = string.Empty;
        _details.Text = string.Empty;
        _busy = true;
        _query.IsEnabled = _input.IsEnabled = false;
        _status.Text = UiText.T("正在查询…");
        try
        {
            var row = await _api.LookupAsync(sn, _settings.UploadApiKey, token);
            if (token.IsCancellationRequested) return;
            if (row is null) _status.Text = UiText.T("未找到该 SN 的托盘记录，请确认 SN 和上传数据。");
            else
            {
                _status.Text = UiText.T("对应托盘");
                _pallet.Text = UiText.F("托盘 {0}", row.PalletNumber);
                _details.Text = UiText.F("SN：{0}\n扫描时间：{1:yyyy-MM-dd HH:mm:ss}", row.Sn, row.ScanTime);
            }
        }
        catch (OperationCanceledException)
        {
            if (!token.IsCancellationRequested) _status.Text = UiText.T("查询超时，请重试。");
        }
        catch (HttpRequestException) { _status.Text = UiText.T("无法连接服务器，请检查网络后重试。"); }
        catch (Exception ex) { _status.Text = UiText.Error(ex); }
        finally
        {
            _busy = false;
            _query.IsEnabled = _input.IsEnabled = true;
            _input.Text = string.Empty;
            if (_lifetime is not null) _input.Focus();
        }
    }
}
