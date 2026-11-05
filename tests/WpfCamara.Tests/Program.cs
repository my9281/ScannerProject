using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WPF.Camara;
using ZXing;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        DirectPrintTests.Run();
        ReceivingTests.Run();
        var items = LabelCatalog.Create();
        if (items.Count != 29 || items.Sum(x => x.Quantity) != 253 || items.Count(x => x.Quantity == 0) != 4) throw new Exception("清单数量错误");
        if (items[0].Sku != "F5100DJKABTX-40M-7C" || items[3].Sku != "F5800ADJKABTX-100M-5C" || items[18].Sku != "S8951DM-30M" || items[26].Sku != "S68R") throw new Exception("SKU 被改写");
        var jobs = LabelCatalog.BuildJobs(items);
        if (jobs.Count != 25 || jobs.Sum(x => x.Copies) != 253) throw new Exception("批量份数错误");
        var document = MainWindow.CreateDocument(jobs, 378, 265);
        if (document.Pages.Count != 253) throw new Exception("打印页数错误");
        int pageIndex = 0;
        foreach (var job in jobs)
        for (int i = 0; i < job.Copies; i++)
        {
            var label = document.Pages[pageIndex++].Child.Children[0];
            if (!TextOf(label).SequenceEqual(new[] { job.Sku })) throw new Exception("打印页内容或顺序错误");
        }
        var variants = CameraModel.All.SelectMany(x => x.VariantSkus).ToArray();
        if (!variants.Contains("S840DSM-7C-100M") || !variants.Contains("F5800ADJKABTX-70M-5C") || !variants.Contains("S850DSTKMS68-7C-50M")) throw new Exception("缺少图片规格");
        foreach (var item in items.Concat(variants.Select(x => new LabelItem(x, "", 0))).DistinctBy(x => x.Sku))
        {
            var label = MainWindow.CreateLabel(378, 265, item.Sku);
            if (!TextOf(label).SequenceEqual(new[] { item.Sku })) throw new Exception("标签含核对码或其他文字");
            label.Measure(new Size(label.Width, label.Height));
            label.Arrange(new Rect(0, 0, label.Width, label.Height));
            label.UpdateLayout();
            var bitmap = new RenderTargetBitmap(1134, 795, 288, 288, PixelFormats.Pbgra32);
            bitmap.Render(label);
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            var result = new BarcodeReaderGeneric().Decode(new RGBLuminanceSource(pixels, bitmap.PixelWidth, bitmap.PixelHeight, RGBLuminanceSource.BitmapFormat.BGRA32));
            if (result?.Text != item.Sku) throw new Exception($"二维码错误：{item.Sku}");
            Directory.CreateDirectory("artifacts/camara/catalog");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create($"artifacts/camara/catalog/{item.Sku}.png");
            encoder.Save(stream);
        }
        items[0].Quantity = 2;
        if (LabelCatalog.BuildJobs(items)[0].Copies != 2) throw new Exception("修改数量未生效");
        items[0].Quantity = -1;
        try { LabelCatalog.BuildJobs(items); throw new Exception("未拒绝负数"); } catch (ArgumentException) { }
        if (LabelCatalog.BuildJobs([new("S68R", "", 0)]).Count != 0) throw new Exception("零数量未跳过");
        var window = new MainWindow();
        ((TabControl)window.FindName("Mode")).SelectedIndex = 0;
        var grid = (DataGrid)window.FindName("Catalog");
        grid.SelectedIndex = 26;
        if (((TextBox)window.FindName("Copies")).Text != "33") throw new Exception("选择行未更新份数");
        ((TextBox)window.FindName("LabelWidth")).Text = "NaN";
        if (((ContentControl)window.FindName("Preview")).Content != null) throw new Exception("无效尺寸仍显示预览");
        var mode = (TabControl)window.FindName("Mode");
        var models = (ComboBox)window.FindName("Sku");
        var specifications = (ComboBox)window.FindName("Meters");
        ((TextBox)window.FindName("LabelWidth")).Text = "100";
        mode.SelectedIndex = 1;
        foreach (var model in CameraModel.All)
        {
            models.SelectedItem = model;
            if (!specifications.Items.Cast<string>().SequenceEqual(model.VariantSkus)) throw new Exception("型号切换后规格错误");
            foreach (string variant in model.VariantSkus)
            {
                specifications.SelectedItem = variant;
                var previewLabel = (FrameworkElement)((ContentControl)window.FindName("Preview")).Content;
                if (!TextOf(previewLabel).SequenceEqual(new[] { variant })) throw new Exception("选中规格未更新标签");
                if (((TextBlock)window.FindName("Configuration")).Text.Contains("内存卡")) throw new Exception("不应显示内存卡");
            }
        }
        if (!CameraModel.Describe("S840DSM-7C-100M").Contains("电缆直径：7 mm") || !CameraModel.Describe("F5800ADJKABTX-100M-5C").Contains("电缆直径：5 mm")) throw new Exception("电缆描述未使用SKU规格");
        if (!CameraModel.Describe("S850DSTKMS68-7C-30M").Contains("探测器：有") || !CameraModel.Describe("S850DSTKM-7C-30M").Contains("探测器：/")) throw new Exception("探测器配置错误");
        if (!CameraModel.Describe("S840DSM-5C-50M").Contains("发射器：/") || !CameraModel.Describe("F5100DJKABTX-40M-7C").Contains("扬声器：有")) throw new Exception("功能配置错误");
        window.Close();
        Console.WriteLine("PASS: 29 项清单及全部型号规格渲染、二维码解码；25 项 / 253 张批量页内容与顺序；型号切换及配置说明；核对码与内存卡排除；数量和尺寸校验。");
    }
    private static IEnumerable<string> TextOf(DependencyObject node)
    {
        if (node is TextBlock text) yield return text.Text;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            foreach (var value in TextOf(VisualTreeHelper.GetChild(node, i))) yield return value;
    }
}



