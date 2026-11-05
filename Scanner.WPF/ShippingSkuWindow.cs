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
        public ShippingSkuWindow(string initialQuery = "")
        {
            Title = "发货 SKU 尺寸与类型查询"; Width = 1200; Height = 800;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var panel = new DockPanel { Margin = new Thickness(12) }; Content = panel;
            var intro = new TextBlock { Text = "输入 SKU、型号、描述/备注或词条进行模糊查询；空格分隔的关键词需同时匹配。\n包装尺寸：CM，重量：KG；空白表示原表未提供。数据版本：2026-11-03，仅供发货核对。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
            DockPanel.SetDock(intro, Dock.Top); panel.Children.Add(intro);
            var search = new TextBox { Text = initialQuery ?? "", Padding = new Thickness(6), Margin = new Thickness(0, 0, 0, 8) };
            DockPanel.SetDock(search, Dock.Top); panel.Children.Add(search);
            var count = new TextBlock { Text = "正在加载…", Margin = new Thickness(0, 0, 0, 8) };
            DockPanel.SetDock(count, Dock.Top); panel.Children.Add(count);
            var details = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 180, Margin = new Thickness(0, 8, 0, 0) };
            DockPanel.SetDock(details, Dock.Bottom); panel.Children.Add(details);
            var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single, EnableRowVirtualization = true, EnableColumnVirtualization = true };
            foreach (var column in new[] { ("SKU", "Sku"), ("描述/备注", "Description"), ("类别", "Category"), ("型号", "Model"), ("长 CM", "Length"), ("宽 CM", "Width"), ("高 CM", "Height"), ("重量 KG", "Weight") })
                grid.Columns.Add(new DataGridTextColumn { Header = column.Item1, Binding = new Binding(column.Item2), Width = column.Item2 == "Description" ? new DataGridLength(320) : DataGridLength.Auto });
            panel.Children.Add(grid);
            grid.SelectionChanged += (_, _) => details.Text = (grid.SelectedItem as ShippingSkuEntry)?.Details ?? "";
            bool ready = false;
            void Refresh()
            {
                if (!ready) return;
                var rows = ShippingSkuCatalog.Search(search.Text); grid.ItemsSource = rows;
                count.Text = $"匹配 {rows.Length:N0} / {ShippingSkuCatalog.Entries.Count:N0} 条；选择词条可查看全部原始字段。";
            }
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            timer.Tick += (_, _) => { timer.Stop(); Refresh(); };
            search.TextChanged += (_, _) => { timer.Stop(); timer.Start(); };
            Loaded += async (_, _) =>
            {
                try { await Task.Run(() => ShippingSkuCatalog.Entries); ready = true; Refresh(); search.Focus(); }
                catch (Exception ex) { count.Text = "加载失败：" + ex.Message; }
            };
            Closed += (_, _) => timer.Stop();
        }
    }
}
