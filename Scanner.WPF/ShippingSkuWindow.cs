using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Scanner.WPF.Services;

namespace Scanner.WPF
{
    public sealed class ShippingSkuWindow : Window
    {
        public ShippingSkuEntry SelectedSku { get; private set; }

        public ShippingSkuWindow(string initialQuery = "")
        {
            Title = "发货 SKU 尺寸与类型查询"; Width = 1200; Height = 800;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var panel = new DockPanel { Margin = new Thickness(12) }; Content = panel;
            var intro = new TextBlock { Text = "输入 SKU、型号、描述或备注进行模糊查询，可用空格组合关键词。\n列表显示向上取整的英寸和磅，详情保留原始 CM / KG。应用后，缺失或无效的数值会留空，可在面单表单补填。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
            DockPanel.SetDock(intro, Dock.Top); panel.Children.Add(intro);
            var search = new TextBox { Text = initialQuery ?? "", Padding = new Thickness(6), Margin = new Thickness(0, 0, 0, 8) };
            DockPanel.SetDock(search, Dock.Top); panel.Children.Add(search);
            var count = new TextBlock { Text = "正在加载…", Margin = new Thickness(0, 0, 0, 8) };
            DockPanel.SetDock(count, Dock.Top); panel.Children.Add(count);
            var apply = new Button
            {
                Content = "应用到出库面单", IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Right,
                Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 8, 0, 0)
            };
            DockPanel.SetDock(apply, Dock.Bottom); panel.Children.Add(apply);
            var details = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 180, Margin = new Thickness(0, 8, 0, 0) };
            DockPanel.SetDock(details, Dock.Bottom); panel.Children.Add(details);
            var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single, EnableRowVirtualization = true, EnableColumnVirtualization = true };
            foreach (var column in new[] { ("SKU", "Sku"), ("描述/备注", "Description"), ("类别", "Category"), ("型号", "Model"), ("长 IN", "LengthInches"), ("宽 IN", "WidthInches"), ("高 IN", "HeightInches"), ("重量 LB", "WeightPounds") })
                grid.Columns.Add(new DataGridTextColumn { Header = column.Item1, Binding = new Binding(column.Item2), Width = column.Item2 == "Description" ? new DataGridLength(320) : DataGridLength.Auto });
            panel.Children.Add(grid);
            grid.SelectionChanged += (_, _) =>
            {
                var selected = grid.SelectedItem as ShippingSkuEntry;
                details.Text = selected?.Details ?? "";
                apply.IsEnabled = selected?.HasShippingMeasurements == true;
            };
            apply.Click += (_, _) =>
            {
                SelectedSku = (ShippingSkuEntry)grid.SelectedItem;
                DialogResult = true;
            };
            bool ready = false;
            void Refresh()
            {
                if (!ready) return;
                var rows = ShippingSkuCatalog.Search(search.Text); grid.ItemsSource = rows;
                count.Text = $"匹配 {rows.Length:N0} / {ShippingSkuCatalog.Entries.Count:N0} 条；选择词条可查看全部原始字段。";
            }
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            timer.Tick += (_, _) => { timer.Stop(); Refresh(); };
            search.TextChanged += (_, _) =>
            {
                grid.ItemsSource = null;
                timer.Stop();
                timer.Start();
            };
            Loaded += async (_, _) =>
            {
                try { await Task.Run(() => ShippingSkuCatalog.Entries); ready = true; Refresh(); search.Focus(); }
                catch (Exception ex) { count.Text = "加载失败：" + ex.Message; }
            };
            Closed += (_, _) => timer.Stop();
        }
    }
}
