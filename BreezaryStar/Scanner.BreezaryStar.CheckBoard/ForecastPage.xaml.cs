using Scanner.BreezaryStar.CheckBoard.Models;
using Scanner.BreezaryStar.CheckBoard.Services;

namespace Scanner.BreezaryStar.CheckBoard;

public partial class ForecastPage : ContentPage
{
    private readonly AccountClient accounts;
    private IReadOnlyList<ForecastRow> rows = [];
    private CancellationTokenSource? cancellation;
    private bool loaded;

    public ForecastPage(AccountClient accounts)
    {
        this.accounts = accounts;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!loaded) await RefreshAsync();
    }

    protected override void OnDisappearing()
    {
        cancellation?.Cancel();
        base.OnDisappearing();
    }

    private async void OnRefreshClicked(object? sender, EventArgs e) => await RefreshAsync();
    private async Task RefreshAsync()
    {
        if (cancellation is not null) return;
        using var current = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        cancellation = current;
        RefreshButton.IsEnabled = false;
        BusyIndicator.IsRunning = true;
        StatusLabel.Text = "正在读取服务器本地预报…";
        try
        {
            var downloaded = await accounts.DownloadForecastsAsync(current.Token);
            rows = downloaded;
            loaded = true;
            Filter();
            StatusLabel.Text = $"本地预报已更新 · {DateTime.Now:HH:mm:ss}";
        }
        catch (OperationCanceledException) { StatusLabel.Text = "读取已取消或超时，可重新刷新。"; }
        catch (Exception ex) { StatusLabel.Text = "读取失败：" + ex.Message; }
        finally
        {
            cancellation = null;
            RefreshButton.IsEnabled = true;
            BusyIndicator.IsRunning = false;
        }
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e) => Filter();
    private void Filter()
    {
        var search = SearchInput.Text?.Trim() ?? "";
        var visible = rows.Where(row => search.Length == 0 || string.Join(" ", row.Heading, row.Transport, row.Status, row.Notes)
            .Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
        ForecastList.ItemsSource = visible;
        CountLabel.Text = $"收货预报 · {visible.Length} / {rows.Count} 条";
    }
}
