using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using ScannerProject.BreezaryStar.WPF.Services;

namespace ScannerProject.BreezaryStar.WPF;

public partial class MainWindow : Window
{
    private CancellationTokenSource? readCancellation;

    public MainWindow()
    {
        InitializeComponent();
        ServerInput.Text = Environment.GetEnvironmentVariable("BREEZARYSTAR_SERVER_URL") ?? "https://wms.ymforever.com";
        Closed += (_, _) => readCancellation?.Cancel();
    }

    private async void LoadData_Click(object sender, RoutedEventArgs e)
    {
        var password = PasswordInput.Password;
        if (string.IsNullOrWhiteSpace(UsernameInput.Text) || string.IsNullOrEmpty(password))
        {
            StatusText.Text = "请填写 Scanner 管理员账号及密码。腾讯文档授权请在“服务器授权管理”中完成。";
            UsernameInput.Focus();
            return;
        }
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        readCancellation = cancellation;
        SetLoading(true);
        RecordsGrid.ItemsSource = null;
        var elapsed = Stopwatch.StartNew();
        try
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(40) };
            var reader = new ScannerServerReader(http);
            var progress = new Progress<string>(message => StatusText.Text = message);
            var records = await reader.ReadAsync(ScannerServerReader.ServerUri(ServerInput.Text), UsernameInput.Text, password,
                progress, cancellation.Token);
            RecordsGrid.ItemsSource = records.DefaultView;
            StatusText.Text = $"读取成功 · {records.Rows.Count} 条记录 · {elapsed.Elapsed.TotalSeconds:F1} 秒 · {DateTime.Now:HH:mm:ss}";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "读取已取消或超时，请检查网络后重试。";
        }
        catch (Exception ex)
        {
            var message = ex.Message.Replace(password, "[密码已隐藏]", StringComparison.Ordinal);
            StatusText.Text = "读取失败 · " + message[..Math.Min(message.Length, 1600)];
        }
        finally
        {
            readCancellation = null;
            SetLoading(false);
        }
    }

    private void SetLoading(bool loading)
    {
        LoadButton.IsEnabled = !loading;
        CancelButton.IsEnabled = loading;
        PasswordInput.IsEnabled = UsernameInput.IsEnabled = ServerInput.IsEnabled = !loading;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => readCancellation?.Cancel();

    private void OpenAuthorization_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var uri = new Uri(ScannerServerReader.ServerUri(ServerInput.Text), "tencent-docs.html");
            Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusText.Text = "无法打开服务器授权管理：" + ex.Message;
        }
    }
}
