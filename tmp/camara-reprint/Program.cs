using System.IO;
using System.Printing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WPF.Camara;
using ZXing;
using ZXing.Common;

internal static class Program
{
    private const int Dpi = 203;
    private const string Printer = "ZPL RP425 Rongta";

    [STAThread]
    private static void Main(string[] args)
    {
        var directory = args[0];
        var jobs = new[] { ("S8951DSRTMS68-50M", "X004HIRJF1") };
        foreach (var (sku, code) in jobs)
        {
            if (args.Contains("--send")) continue;
            if (CameraModel.Describe(sku).StartsWith("此 SKU")) throw new InvalidOperationException(sku);
            var label = (Border)ReceivingLabel.Create(new ReceivingRow(1, 1, code, 1, code, sku));
            var body = (StackPanel)label.Child;
            ((TextBlock)body.Children[0]).Text = "检测出库";
            ((TextBlock)body.Children[1]).Text = "检测出库时间：2026/11/18";
            body.Children.RemoveAt(2);
            while (body.Children.Count > 10) body.Children.RemoveAt(10);
            var data = ZplLabelPrinter.Encode(label, Dpi);
            var width = 4 * Dpi;
            var height = 6 * Dpi;
            var stride = (width + 7) / 8;
            var raster = new byte[stride * height];
            foreach (Match strip in Regex.Matches(Encoding.ASCII.GetString(data), @"\^FO0,(\d+)\^GFA,\d+,\d+,\d+,([0-9A-F]+)\^FS"))
                Convert.FromHexString(strip.Groups[2].Value).CopyTo(raster, int.Parse(strip.Groups[1].Value) * stride);

            var pixels = new byte[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = (raster[y * stride + x / 8] & (128 >> (x % 8))) == 0 ? (byte)255 : (byte)0;

            foreach (var (name, format, expected) in new[] { ("ProductBarcode", BarcodeFormat.CODE_128, code), ("SkuQrCode", BarcodeFormat.QR_CODE, sku) })
            {
                var image = Images(label).Single(i => i.Name == name);
                var origin = image.TransformToAncestor(label).Transform(new Point());
                int left = (int)(origin.X * Dpi / 96), top = (int)(origin.Y * Dpi / 96);
                int w = (int)Math.Round(image.ActualWidth * Dpi / 96), h = (int)Math.Round(image.ActualHeight * Dpi / 96);
                var crop = new byte[w * h];
                for (int y = 0; y < h; y++) Array.Copy(pixels, (top + y) * width + left, crop, y * w, w);
                var reader = new BarcodeReaderGeneric { Options = new DecodingOptions { TryHarder = true, PossibleFormats = [format] } };
                if (reader.Decode(new RGBLuminanceSource(crop, w, h, RGBLuminanceSource.BitmapFormat.Gray8))?.Text != expected)
                    throw new InvalidOperationException($"Decode failed: {sku} {name}");
            }

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(width, height, Dpi, Dpi, PixelFormats.Gray8, null, pixels, width)));
            using var stream = File.Create(Path.Combine(directory, code + ".png"));
            encoder.Save(stream);
            File.WriteAllBytes(Path.Combine(directory, code + ".zpl"), data);
            Console.WriteLine($"Verified {sku} {code}");
        }

        if (!args.Contains("--send")) return;
        using var server = new LocalPrintServer();
        using var queue = server.GetPrintQueue(Printer);
        queue.Refresh();
        Console.WriteLine($"Queue status: {queue.QueueStatus}");
        foreach (var (sku, code) in jobs)
        {
            var data = File.ReadAllBytes(Path.Combine(directory, code + ".zpl"));
            var jobId = ZplLabelPrinter.Send(Printer, $"Camara {code} 2026-11-18", data);
            Console.WriteLine($"Submitted {sku} {code}: {jobId}");
        }
    }

    private static IEnumerable<Image> Images(DependencyObject parent)
    {
        if (parent is Image image) yield return image;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        foreach (var child in Images(VisualTreeHelper.GetChild(parent, i)))
            yield return child;
    }
}


