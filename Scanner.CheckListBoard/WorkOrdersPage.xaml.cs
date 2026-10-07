using System.Collections.ObjectModel;
using System.ComponentModel;
using Scanner.CheckListBoard.Models;
using Scanner.CheckListBoard.Services;

namespace Scanner.CheckListBoard;

public partial class WorkOrdersPage : ContentPage
{
    private readonly AccountClient accounts;
    private readonly HashSet<ulong> pending = [];
    private bool loaded, dirty, saving;
    public ObservableCollection<WorkOrderRow> Orders { get; } = [];
    public WorkOrdersPage(AccountClient accounts)
    {
        this.accounts = accounts;
        InitializeComponent();
        OrdersTable.ItemsSource = Orders;
        SaveButton.IsEnabled = false;
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!accounts.HasPermission("checklist.read"))
        {
            if (Window is { } window) window.Page = new LoginPage(accounts);
            return;
        }
        if (loaded) return;
        await DownloadAsync();
    }
    private async Task DownloadAsync()
    {
        SaveButton.IsEnabled = RefreshButton.IsEnabled = false;
        OrdersTable.IsEnabled = false;
        SaveStatusLabel.Text = "正在下载工单……";
        try
        {
            var downloaded = await accounts.DownloadWorkOrdersAsync();
            foreach (var existing in Orders) existing.PropertyChanged -= OnRowChanged;
            Orders.Clear();
            pending.Clear();
            dirty = false;
            foreach (var row in downloaded)
            {
                row.CanEdit = accounts.HasPermission("checklist.write");
                row.PropertyChanged += OnRowChanged;
                Orders.Add(row);
            }
            CountLabel.Text = $"工单 · {Orders.Count} 条";
            SaveStatusLabel.Text = accounts.HasPermission("checklist.write") ? "已下载审核位为0的工单" : "只读用户";
            loaded = true;
        }
        catch (Exception exception)
        {
            SaveStatusLabel.Text = "工单下载失败";
            await DisplayAlertAsync("下载失败", ErrorMessage(exception), "确定");
        }
        finally
        {
            SaveButton.IsEnabled = loaded && accounts.HasPermission("checklist.write");
            RefreshButton.IsEnabled = true;
            OrdersTable.IsEnabled = true;
        }
    }
    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(WorkOrderRow.IsCompleted)) return;
        if (sender is WorkOrderRow row) pending.Add(row.Id);
        dirty = true;
        SaveStatusLabel.Text = "有未保存的修改";
        SaveStatusLabel.TextColor = Color.FromArgb("#D9B866");
    }
    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (saving || !accounts.HasPermission("checklist.write")) return;
        saving = true;
        SaveButton.IsEnabled = RefreshButton.IsEnabled = false;
        OrdersTable.IsEnabled = false;
        try
        {
            foreach (var row in Orders.Where(x => pending.Contains(x.Id)).ToArray())
            {
                var updated = await accounts.UpdateWorkOrderAsync(row);
                row.Version = updated.Version;
                row.CompletedAt = updated.CompletedAt;
                row.UpdatedAt = updated.UpdatedAt;
                pending.Remove(row.Id);
            }
            dirty = false;
            SaveStatusLabel.Text = "已保存到服务器";
            SaveStatusLabel.TextColor = Color.FromArgb("#43C786");
        }
        catch (Exception exception)
        {
            dirty = pending.Count > 0;
            SaveStatusLabel.Text = "部分工单未保存";
            await DisplayAlertAsync("保存未完成", ErrorMessage(exception) + " 已成功的工单会保留，其他修改尚未提交。", "确定");
            SaveStatusLabel.TextColor = Color.FromArgb("#E53935");
        }
        finally
        {
            saving = false;
            RefreshButton.IsEnabled = true;
            SaveButton.IsEnabled = accounts.HasPermission("checklist.write");
            OrdersTable.IsEnabled = true;
        }
    }
    private static string ErrorMessage(Exception exception) => exception switch
    {
        InvalidOperationException => exception.Message,
        HttpRequestException => "无法连接服务器，请检查网络。",
        TaskCanceledException => "请求超时，请稍后重试。",
        _ => "工单请求失败，请稍后重试。"
    };
    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        if (saving) return;
        if (dirty && !await DisplayAlertAsync("尚未保存", "重新下载会放弃当前未保存的修改，继续吗？", "重新下载", "继续编辑")) return;
        await DownloadAsync();
    }
    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (saving) return;
        if (dirty && !await DisplayAlertAsync("尚未保存", "返回将放弃本次修改，确定返回吗？", "放弃并返回", "继续编辑")) return;
        await Navigation.PopAsync();
    }
    protected override bool OnBackButtonPressed()
    {
        if (!dirty && !saving) return base.OnBackButtonPressed();
        if (!saving) Dispatcher.Dispatch(() => OnBackClicked(this, EventArgs.Empty));
        return true;
    }
}
