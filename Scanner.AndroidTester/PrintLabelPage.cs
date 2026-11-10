using Scanner.AndroidTester.Services;
namespace Scanner.AndroidTester;
public sealed class PrintLabelPage : ContentPage
{
    private readonly Entry _input = new() { Placeholder = UiText.T("扫描条码后回车，自动打印"), ReturnType = ReturnType.Done, FontSize = 22, TextColor = Color.FromArgb("#F2F2F2"), BackgroundColor = Color.FromArgb("#181C20"), IsTextPredictionEnabled = false };
    private readonly Label _status = new() { Text = "", TextColor = Color.FromArgb("#92979D") };
    private bool _active;
    private bool _printing;

    public PrintLabelPage(LatestScan latest)
    {
        Title = UiText.T("打印标签");
        BackgroundColor = Color.FromArgb("#080A0C");
        var button = new Button { Text = UiText.T("打印"), FontSize = 24, HeightRequest = 64, Margin = 24, VerticalOptions = LayoutOptions.Center };
#if ANDROID
        SemanticProperties.SetHint(button, UiText.T("长按可更换打印机或指令类型"));
        async void ResetPrinter(object? sender, Android.Views.View.LongClickEventArgs args)
        {
            args.Handled = true;
            if (!button.IsEnabled) return;
            Services.RongtaBluetoothPrinter.ResetSelection();
            await DisplayAlertAsync(UiText.T("打印设置"), UiText.T("已清除打印机选择，下次点击打印时重新选择。"), UiText.T("确定"));
        }
        button.HandlerChanging += (_, args) => { if (args.OldHandler?.PlatformView is Android.Views.View view) view.LongClick -= ResetPrinter; };
        button.HandlerChanged += (_, _) => { if (button.Handler?.PlatformView is Android.Views.View view) view.LongClick += ResetPrinter; };
#endif
        async Task PrintAsync()
        {
            if (_printing) return;
            var sn = _input.Text?.Trim();
            if (string.IsNullOrWhiteSpace(sn)) { _status.Text = UiText.T("请在本页扫描条码。"); _input.Focus(); return; }
            _printing = true;
            latest.Record(sn);
            button.IsEnabled = false;
            _input.IsEnabled = false;
            try
            {
#if ANDROID
                button.Text = UiText.T("正在连接并发送…");
                _status.Text = UiText.F("正在打印：{0}", sn);
                if (await Services.RongtaBluetoothPrinter.PrintAsync(this, sn))
                {
                    _input.Text = "";
                    _status.Text = UiText.F("已发送：{0}。请确认出纸，可继续扫码。", sn);
                }
                else _status.Text = UiText.T("已取消，请回车重试。");
#else
                _status.Text = UiText.T("此打印入口用于 Android 蓝牙打印。");
#endif
            }
            catch (Exception ex)
            {
                _status.Text = UiText.F("打印未完成：{0}。请检查出纸后再重试。", sn);
#if ANDROID
                bool reset = await DisplayAlertAsync(UiText.T("打印未完成"), UiText.Error(ex) + UiText.T("\n若打印机已出纸，请勿重复打印。是否重新选择打印机或指令类型？"), UiText.T("重新选择"), UiText.T("关闭"));
                if (reset) Services.RongtaBluetoothPrinter.ResetSelection();
#else
                await DisplayAlertAsync(UiText.T("打印失败"), UiText.Error(ex), UiText.T("确定"));
#endif
            }
            finally
            {
                button.Text = UiText.T("打印");
                button.IsEnabled = true;
                _input.IsEnabled = true;
                _printing = false;
                FocusInput();
            }
        }
        button.Clicked += async (_, _) => await PrintAsync();
        _input.Completed += async (_, _) => await PrintAsync();
        Content = new VerticalStackLayout { Padding = 16, Spacing = 16, Children = { _input, _status, button } };
    }

    protected override void OnAppearing() { base.OnAppearing(); _active = true; FocusInput(); }
    protected override void OnDisappearing() { _active = false; base.OnDisappearing(); }
    private void FocusInput() => Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(200), () => { if (_active && !_printing) _input.Focus(); });
}
