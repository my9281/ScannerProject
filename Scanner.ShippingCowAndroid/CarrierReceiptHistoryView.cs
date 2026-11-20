using Scanner.ShippingCowAndroid.Services;

namespace Scanner.ShippingCowAndroid;

public sealed class CarrierReceiptHistoryView : ContentView
{
    private readonly CarrierReceivingQueue queue;
    private readonly ListView records = new() { HasUnevenRows = true, SelectionMode = ListViewSelectionMode.None, BackgroundColor = Colors.Transparent };
    private readonly Label count = new() { FontSize = 14 };
    private readonly Label message = new() { TextColor = Color.FromArgb("#B42318"), FontSize = 13 };
    public CarrierReceiptHistoryView(CarrierReceivingQueue queue)
    {
        this.queue = queue;
        records.ItemTemplate = new DataTemplate(() =>
        {
            var text = new Label { FontSize = 14, LineBreakMode = LineBreakMode.CharacterWrap };
            text.SetBinding(Label.TextProperty, nameof(Row.Text));
            text.SetBinding(Label.TextColorProperty, nameof(Row.Color));
            var delete = new Button { Text = UiText.T("删除"), FontSize = 13, Padding = new Thickness(10, 6) };
            delete.Clicked += (_, _) => { if (delete.BindingContext is Row row) Change(() => queue.Delete(row.Id), UiText.T("已删除本机记录。")); };
            var grid = new Grid { Padding = new Thickness(0, 10), ColumnSpacing = 8,
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
            grid.Add(text, 0, 0); grid.Add(delete, 1, 0);
            return new ViewCell { View = grid };
        });
        var layout = new Grid { RowSpacing = 6, RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) } };
        layout.Add(count, 0, 0); layout.Add(message, 0, 1); layout.Add(records, 0, 2);
        Content = layout; Refresh();
    }
    private sealed record Row(Guid Id, string Text, Color Color);
    public event Action? Changed;
    private void Change(Action action, string success)
    {
        try { action(); message.Text = success; Refresh(); Changed?.Invoke(); }
        catch (Exception ex) { message.Text = ex is InvalidOperationException ? UiText.Error(ex) : UiText.T("保存失败，请重试。"); }
    }
    public void Refresh()
    {
        count.Text = UiText.F("收货记录 {0} 条 · 待上传 {1} 条", queue.Rows.Count, queue.Pending.Count);
        records.ItemsSource = queue.Rows.Reverse().Select(r => new Row(r.Scan.ScanId,
            $"{r.Scan.TrackingNumber}\n{FormatRowDetails(r)}",
            Color.FromArgb(r.Warning is not null ? "#B42318" : "#1A202C"))).ToArray();
    }

    private static string FormatRowDetails(CarrierReceiptLocalRow row)
    {
        var time = row.Scan.ScannedAt.ToLocalTime().ToString("MM-dd HH:mm:ss");
        var status = row.Warning is not null ? UiText.T("请重扫") : UiText.T(row.Uploaded ? "已上传" : "待上传");
        return string.IsNullOrEmpty(row.Scan.Carrier)
            ? $"{time} · {status}"
            : $"{Scanner.Server.Model.CarrierReceiptValidation.CarrierName(row.Scan.Carrier)} · {time} · {status}";
    }
}
