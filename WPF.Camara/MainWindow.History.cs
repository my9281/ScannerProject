using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;

namespace WPF.Camara;

public partial class MainWindow
{
    private void ViewLabelHistory_Click(object sender, RoutedEventArgs e)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Data", "history");
        var dialog = new OpenFileDialog { Filter = "历史标签数据 (*.json)|*.json", Title = "查看历史标签数据（只读）",
            InitialDirectory = Directory.Exists(folder) ? folder : AppContext.BaseDirectory };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var data = LabelDataArchive.Parse(File.ReadAllText(dialog.FileName));
            var tabs = new TabControl();
            tabs.Items.Add(new TabItem { Header = $"核对明细（{data.ForecastRows.Length} 行）", Content = HistoryGrid(data.ForecastRows,
                ("行号", "Line"), ("托盘/分组", "Pallet"), ("原始码", "OriginalCode"), ("校正码", "Barcode"), ("完整 SKU", "Sku"), ("数量", "Quantity")) });
            tabs.Items.Add(new TabItem { Header = $"贴标清单（{data.LabelCatalog.Length} 项）", Content = HistoryGrid(data.LabelCatalog,
                ("完整 SKU", "Sku"), ("核对码", "CheckCodes"), ("数量", "Quantity")) });
            tabs.Items.Add(new TabItem { Header = "型号规格", Content = HistoryGrid(data.CameraModels.Select(x => new
                { x.Sku, x.Transmitter, x.Detector, x.WhiteBalance, x.Speaker, Specifications = string.Join(", ", x.Specifications) }),
                ("型号", "Sku"), ("发射器", "Transmitter"), ("探测器", "Detector"), ("白平衡", "WhiteBalance"), ("扬声器", "Speaker"), ("规格", "Specifications")) });
            var panel = new DockPanel { Margin = new Thickness(16) };
            var summary = new TextBlock { Text = $"历史数据 · 只读，不加入当前打印任务\n{Path.GetFileName(dialog.FileName)}\n核对明细 {data.ForecastRows.Sum(x => x.Quantity)} 件；贴标清单 {data.LabelCatalog.Sum(x => x.Quantity)} 件（两者为独立清单，不相加）",
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
            DockPanel.SetDock(summary, Dock.Top); panel.Children.Add(summary); panel.Children.Add(tabs);
            new Window { Owner = this, Title = "历史标签数据 · " + Path.GetFileName(dialog.FileName), Width = 1100, Height = 720,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel }.ShowDialog();
        }
        catch (Exception ex) { Status.Text = "历史数据读取失败，当前打印数据未改变：" + ex.Message; }
    }

    private static DataGrid HistoryGrid(System.Collections.IEnumerable source, params (string Label, string Field)[] columns)
    {
        var grid = new DataGrid { IsReadOnly = true, AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, ItemsSource = source };
        foreach (var column in columns) grid.Columns.Add(new DataGridTextColumn { Header = column.Label, Binding = new Binding(column.Field), Width = DataGridLength.Auto });
        return grid;
    }
}
