using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WPF.Camara;
using ZXing;
using ZXing.Common;

internal static class ReceivingTests
{
    public static void Run()
    {
        var report = CurrentBatchTests.History().CreateForecast();
        Check(report.Pallets.Count == 19 && report.Rows.Count == 158, "预报卡板或行数错误");
        Check(report.Rows.Sum(x => x.Quantity) == 621, "预报总量错误");
        Check(report.Rows.Where(x => x.CanPrint).Sum(x => x.Quantity) == 573, "已知数量错误");
        var loose2 = report.Rows.Single(x => x.Pallet == -2);
        Check(loose2.PalletName == "散货2" && loose2.OriginalCode == "SANYI-S68R" && loose2.Barcode == "S68R" && loose2.Sku == "S68R" && loose2.Quantity == 1 && loose2.CanPrint, "散货2与扫描件不一致");
        Check(report.BuildJobs(-2, true).Sum(x => x.Copies) == 1 && report.BuildJobs(-2, false).Count == 1, "散货2打印份数错误");
        var loose = report.Rows.Where(x => x.Pallet == -1).ToArray();
        Check(loose.Length == 17 && loose.All(x => x.CanPrint && x.PalletName == "散货1"), "散货1分组错误");
        Check(loose.Select(x => x.Quantity).SequenceEqual(new[] { 1,1,1,3,1,2,1,1,1,1,1,2,3,5,1,4,4 }), "散货数量与Excel或用户更正不一致");
        Check(loose.All(x => x.OriginalCode == "SANYI-" + x.Barcode), "散货产品代码或条码错误");
        Check(loose.Select(x => x.Sku).SequenceEqual(new[] {
            "S610DSTKMCPS68-7C-50M", "XSP-S5517DC", "S810ASMKT-100M", "S68R",
            "S880DSTKM-7C-50M", "S860DSTKM-7C-30M", "S510DSRKM-7C-50M", "S860DSTKMS68-7C-30M",
            "S810ASMKT-50M", "S860DSTKM-7C-50M", "S880DSTKMS68-7C-30M", "S8417DC-30m",
            "S8951DSRTMS68-30M", "S8951DSRTMS68-50M", "S8951DSRTMS68-20M", "S840DSRTMS68-7C-70M", "S840DSRTMS68-7C-50M"
        }), "散货SKU匹配错误");
        Check(report.BuildJobs(-1, true).Sum(x => x.Copies) == 33 && report.BuildJobs(-1, false).Count == 17, "散货打印份数错误");
        Check(report.Rows.Select(x => x.Line).Distinct().Count() == 158, "源行编号重复");
        Check(ReceivingForecast.Parse("散货1\tX1\t2\tX1\tSKU").Rows.Single().PalletName == "散货1", "散货导入失败");
        Check(report.Rows.Count(x => !x.CanPrint) == 29, "未知行数错误");
        var correction = report.Rows.Single(x => x.OriginalCode == "X004H1QOBZ");
        Check(correction.Barcode == "X004HIQ0BZ", "未保留校对码");
        Check(report.Rows.Single(x => x.OriginalCode == "LPNNL58HDJ7SS").Barcode == "LPNNL58HDJ7SS", "不可推测LPN替代码");
        foreach (int pallet in report.Pallets)
        {
            var jobs = report.BuildJobs(pallet, true);
            Check(jobs.All(x => x.Row.Pallet == pallet && x.Row.CanPrint), "混板或未知行进入打印");
            Check(jobs.Sum(x => x.Copies) == report.Rows.Where(x => x.Pallet == pallet && x.CanPrint).Sum(x => x.Quantity), "整板份数错误");
            var oneEach = report.BuildJobs(pallet, false);
            Check(oneEach.All(x => x.Copies == 1), "每行一张模式错误");
        }
        var firstJobs = report.BuildJobs(1, true);
        var doc = ReceivingLabel.Document(firstJobs);
        Check(doc.DocumentPaginator.PageSize == new Size(384, 576) && doc.Pages.Count == firstJobs.Sum(x => x.Copies), "4x6页数或尺寸错误");
        Check(doc.Pages[0].Child.Width == 384 && doc.Pages[0].Child.Height == 576, "物理页面错误");
        try { ReceivingLabel.Create(report.Rows.First(x => !x.CanPrint)); throw new Exception("未知SKU未拒绝"); } catch (ArgumentException) { }
        foreach (var text in new[] { "bad", "\tX1\t2\tX1\tSKU", "第1个卡板\tX1\t-1\tX1\tSKU" })
        {
            try { ReceivingForecast.Parse(text); throw new Exception("无效预报未拒绝"); } catch (System.FormatException) { }
        }
        Directory.CreateDirectory("artifacts/camara/receiving");
        foreach (var row in report.Rows.Concat(ReceivingForecast.LoadDefault().Rows).Where(x => x.CanPrint))
        {
            var label = ReceivingLabel.Create(row, 1, row.Quantity);
            Layout(label);
            foreach (var text in Descendants(label).OfType<TextBlock>())
            {
                var bottom = text.TransformToAncestor(label).Transform(new Point(0, text.ActualHeight)).Y;
                Check(bottom <= 576, $"标签溢出：{row.Sku} / {text.Text}");
                Check(!text.Text.Contains("内存卡"), "不应打印内存卡描述");
            }
            var bitmap = Render(label);
            foreach (var code in Descendants(label).OfType<Image>())
            {
                var p = code.TransformToAncestor(label).Transform(new Point(0, 0));
                var region = new CroppedBitmap(bitmap, new Int32Rect((int)(p.X * 3), (int)(p.Y * 3), (int)Math.Round(code.ActualWidth * 3), (int)Math.Round(code.ActualHeight * 3)));
                var pixels = new byte[region.PixelWidth * region.PixelHeight * 4];
                region.CopyPixels(pixels, region.PixelWidth * 4, 0);
                var reader = new BarcodeReaderGeneric { Options = new DecodingOptions { TryHarder = true, PossibleFormats = [code.Name == "ProductBarcode" ? BarcodeFormat.CODE_128 : BarcodeFormat.QR_CODE] } };
                var result = reader.Decode(new RGBLuminanceSource(pixels, region.PixelWidth, region.PixelHeight, RGBLuminanceSource.BitmapFormat.BGRA32));
                var expected = code.Name == "ProductBarcode" ? row.Barcode : row.Sku;
                if (result?.Text != expected) { Save(region, "artifacts/camara/receiving/failed-code.png"); Save(bitmap, "artifacts/camara/receiving/failed-page.png"); }
                Check(result?.Text == expected, $"解码失败：行{row.Line} {code.Name} {expected}");
            }
            if (row.Line is 1 or 9 or 24 or 34 or 137 or 141 or 144 or 154 or 158) Save(bitmap, $"artifacts/camara/receiving/row-{row.Line}.png");
        }
        var window = new MainWindow();
        Check(((TabControl)window.FindName("Mode")).SelectedIndex == 2, "未默认打开按板模式");
        var grid = (DataGrid)window.FindName("ReceivingRows");
        Check(grid.Items.Count == 66 && grid.Items.Cast<ReceivingRow>().All(x => x.Pallet == -3), "Current UI must contain batch2 only");
        grid.SelectedIndex = 14;
        Check(((TextBox)window.FindName("Copies")).Text == "18", "Batch2 quantity must populate print copies");
        ((ComboBox)window.FindName("LabelPolicy")).SelectedIndex = 1;
        Check(((TextBox)window.FindName("Copies")).Text == "1", "One-label-per-row policy");
        Check(((TextBlock)window.FindName("PalletSummary")).Text.Contains("261"), "Batch2 quantity summary");
        var root = (FrameworkElement)window.Content; root.Width = 1204; root.Height = 650; Layout(root);
        Save(Render(root), "artifacts/camara/receiving/window.png");
        window.Close();
        Console.WriteLine("PASS: historical labels and batch2 barcodes/QR codes render and decode; current UI shows 66 rows / 261 items with per-piece and per-row policies.");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Layout(FrameworkElement e) { e.Measure(new Size(e.Width,e.Height));e.Arrange(new Rect(0,0,e.Width,e.Height));e.UpdateLayout(); }
    private static RenderTargetBitmap Render(FrameworkElement e) { var bitmap = new RenderTargetBitmap((int)Math.Ceiling(e.ActualWidth*3),(int)Math.Ceiling(e.ActualHeight*3),288,288,PixelFormats.Pbgra32);bitmap.Render(e);return bitmap; }
    private static void Save(BitmapSource bitmap,string path) { var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(path);encoder.Save(stream); }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent) { for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var d in Descendants(child))yield return d;} }
}



