using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WPF.Camara;

public sealed class RepairStartWindow : Window
{
    private readonly TextBox sn = Input();
    private readonly TextBox sku = Input();
    private readonly TextBox address = new() { Margin = new Thickness(0, 4, 0, 10), Height = 30 };
    private readonly PasswordBox key = new() { Margin = new Thickness(0, 4, 0, 10), Height = 30 };
    private readonly Button clearSn = new() { Content = "清除 SN", Padding = new Thickness(14, 7, 14, 7) };
    private readonly Button clearSku = new() { Content = "清除 SKU", Padding = new Thickness(14, 7, 14, 7) };
    private readonly Button submit = new() { Content = "确认开始维修并上传", Padding = new Thickness(20, 12, 20, 12), FontSize = 18 };
    private readonly TextBlock time = new() { Margin = new Thickness(0, 14, 0, 12) };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0), FontSize = 15 };
    private readonly RepairStartSession session;
    private readonly HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(60) };
    private readonly CancellationTokenSource lifetime = new();
    private bool busy;
    private bool closed;

    private static TextBox Input() => new() { Height = 44, FontSize = 22, Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 6, 0, 6) };

    public RepairStartWindow()
    {
        Title = "开始维修 · SN / SKU 录入";
        Width = 650; Height = 720; MinWidth = 540; MinHeight = 650;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(243, 245, 248));
        var settings = RepairStartApi.LoadSettings();
        session = new RepairStartSession(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScannerProject", "Camara", "repair-start-pending.json"));
        address.Text = settings.BaseUrl;
        key.Password = settings.ApiKey;
        sn.Text = session.Sn;
        sku.Text = session.Sku;
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "开始维修", FontSize = 26, FontWeight = FontWeights.Bold });
        panel.Children.Add(new TextBlock { Text = "扫描 SN 后按回车转到 SKU；两项各 1–50 位。上传成功后清空，继续下一台。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 16) });
        panel.Children.Add(new TextBlock { Text = "SN" }); panel.Children.Add(sn); panel.Children.Add(clearSn);
        panel.Children.Add(new TextBlock { Text = "SKU", Margin = new Thickness(0, 14, 0, 0) }); panel.Children.Add(sku); panel.Children.Add(clearSku);
        panel.Children.Add(time); panel.Children.Add(submit); panel.Children.Add(status);
        var configuration = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        configuration.Children.Add(new TextBlock { Text = "服务地址（HTTPS）" }); configuration.Children.Add(address);
        configuration.Children.Add(new TextBlock { Text = "上传密钥（此处输入仅本次使用）" }); configuration.Children.Add(key);
        panel.Children.Add(new Expander { Header = "连接设置", Content = configuration, Margin = new Thickness(0, 18, 0, 0) });
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        sn.TextChanged += (_, _) => UpdateDraft();
        sku.TextChanged += (_, _) => UpdateDraft();
        sn.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; sku.Focus(); sku.SelectAll(); } };
        sku.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; submit.Focus(); } };
        clearSn.Click += (_, _) => { sn.Clear(); sn.Focus(); };
        clearSku.Click += (_, _) => { sku.Clear(); sku.Focus(); };
        submit.Click += async (_, _) => await UploadAsync();
        Loaded += (_, _) => { if (session.Pending is null) sn.Focus(); else submit.Focus(); };
        Closed += (_, _) => { closed = true; lifetime.Cancel(); client.Dispose(); session.Dispose(); lifetime.Dispose(); };
        Refresh();
        status.Text = session.Pending is null ? "请录入 SN 和 SKU。首个编码开始录入时计时；两项全部清空后重新计时。"
            : "已恢复待上传记录。内容已锁定，请重试；相同编号不会重复入库。";
    }

    private void UpdateDraft()
    {
        if (busy || session.Pending is not null) return;
        session.Update(sn.Text, sku.Text, DateTimeOffset.UtcNow);
        Refresh();
        status.Text = (!string.IsNullOrWhiteSpace(sn.Text) && !RepairStartSession.ValidCode(sn.Text))
            || (!string.IsNullOrWhiteSpace(sku.Text) && !RepairStartSession.ValidCode(sku.Text))
            ? "编码不能超过 50 位或包含控制字符，请清除后重新扫描。"
            : session.CanSubmit ? "两项已齐全，请核对后确认开始维修。" : "请补齐 SN 和 SKU。";
    }

    private void Refresh()
    {
        bool editable = !busy && session.Pending is null;
        sn.IsEnabled = sku.IsEnabled = clearSn.IsEnabled = clearSku.IsEnabled = editable;
        address.IsEnabled = key.IsEnabled = !busy;
        submit.IsEnabled = !busy && session.CanSubmit;
        submit.Content = session.Pending is null ? "确认开始维修并上传" : "重试上传开始维修记录";
        time.Text = session.StartedAt is { } started ? $"首扫时间：{started.ToLocalTime():yyyy-MM-dd HH:mm:ss}" : "首扫时间：等待扫描";
    }

    private async Task UploadAsync()
    {
        if (busy || !session.CanSubmit) return;
        // Check connection settings before locking/persisting the business record.
        if (!Uri.TryCreate(address.Text.Trim(), UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment)
            || string.IsNullOrWhiteSpace(key.Password))
        { status.Text = "请展开连接设置，填写有效的 HTTPS 服务地址和上传密钥。"; return; }
        busy = true;
        Refresh();
        status.Text = "正在上传开始维修记录…";
        try
        {
            var record = session.PrepareUpload();
            await new RepairStartApi(client).UploadAsync(endpoint, key.Password, record, lifetime.Token);
            if (closed) return;
            session.Confirm(record.ScanId);
            sn.Clear(); sku.Clear();
            status.Text = $"上传成功：{record.Sn} / {record.Sku}，已记录为开始维修。";
        }
        catch (OperationCanceledException) { if (!closed) status.Text = "上传超时，记录已保留，请重试。"; }
        catch (Exception ex) { if (!closed) status.Text = "上传未完成，当前内容已保留。" + ex.Message; }
        finally
        {
            busy = false;
            if (!closed) { Refresh(); if (session.Pending is null) sn.Focus(); }
        }
    }
}
