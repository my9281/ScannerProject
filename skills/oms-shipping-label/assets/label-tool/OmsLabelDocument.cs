using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Scanner.WPF.Services
{
    public static class OmsLabelDocument
    {
        public const int Dpi = 300;
        public const double Width = 384, Height = 576; // 4 x 6 inches, WPF units.
        // Coordinates in the verified 288 x 432 point FedEx template.
        public static readonly Rect AnnotationAreaPoints = new Rect(170, 60, 86, 42);

        public static async Task<BitmapSource> RenderAsync(string pdf, string rma, string model, string serial)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763)) throw new PlatformNotSupportedException("需要 Windows 10 1809 或更高版本。");
            var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(pdf));
            var document = await PdfDocument.LoadFromFileAsync(file);
            if (document.PageCount != 1) throw new InvalidDataException("当前打印功能仅接受单页 4×6 英寸面单。");
            using var page = document.GetPage(0);
            if (Math.Abs(page.Size.Width - Width) > 2 || Math.Abs(page.Size.Height - Height) > 2) throw new InvalidDataException("面单尺寸不是 4×6 英寸，未自动叠加文字。");
            using var stream = new InMemoryRandomAccessStream();
            await page.RenderToStreamAsync(stream, new PdfPageRenderOptions { DestinationWidth = 1200, DestinationHeight = 1800 });
            using var reader = new DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync((uint)stream.Size);
            var bytes = new byte[(int)stream.Size]; reader.ReadBytes(bytes);
            var decoder = BitmapDecoder.Create(new MemoryStream(bytes), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            return Annotate(decoder.Frames[0], rma, model, serial);
        }
        public static BitmapSource Annotate(BitmapSource original, string rma, string model, string serial)
        {
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                drawing.DrawRectangle(Brushes.White, null, new Rect(0, 0, Width, Height));
                drawing.DrawImage(original, new Rect(0, 0, Width, Height));
                string[] lines = { rma ?? "", model ?? "", serial ?? "" };
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains('\n') || lines[i].Contains('\r')) throw new ArgumentException("每个标注字段只能填写一行。");
                    double font = 12;
                    FormattedText Text() => new FormattedText(lines[i], CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), font, Brushes.Black, 1);
                    var text = Text();
                    while (text.Width > AnnotationAreaPoints.Width * 4 / 3 && font > 8) { font -= .5; text = Text(); }
                    if (text.Width > AnnotationAreaPoints.Width * 4 / 3) throw new ArgumentException("标注内容过长，请缩短后预览。");
                    drawing.DrawText(text, new Point(170 * 4.0 / 3, (61 + i * 11) * 4.0 / 3));
                }
            }
            var output = new RenderTargetBitmap(1200, 1800, Dpi, Dpi, PixelFormats.Pbgra32); output.Render(visual); output.Freeze(); return output;
        }
        public static void SavePng(BitmapSource image, string path)
        {
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = File.Create(path); encoder.Save(stream);
        }
        // Lossless RGB PDF: self-contained, no external PDF renderer/service needed at print time.
        public static void SavePdf(BitmapSource image, string path)
        {
            var rgb = new FormatConvertedBitmap(image, PixelFormats.Rgb24, null, 0);
            var pixels = new byte[rgb.PixelWidth * rgb.PixelHeight * 3]; rgb.CopyPixels(pixels, rgb.PixelWidth * 3, 0);
            using var compressed = new MemoryStream();
            using (var zip = new ZLibStream(compressed, CompressionLevel.Optimal, true)) zip.Write(pixels);
            using var buffer = new MemoryStream();
            void Write(string text) { var data = Encoding.ASCII.GetBytes(text); buffer.Write(data); }
            long[] offsets = new long[6];
            void Object(int id, string content) { offsets[id] = buffer.Position; Write(id + " 0 obj\n" + content + "\nendobj\n"); }
            Write("%PDF-1.4\n");
            Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
            Object(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
            Object(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 288 432] /Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>");
            offsets[4] = buffer.Position;
            Write($"4 0 obj\n<< /Type /XObject /Subtype /Image /Width {rgb.PixelWidth} /Height {rgb.PixelHeight} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /Length {compressed.Length} >>\nstream\n");
            buffer.Write(compressed.ToArray()); Write("\nendstream\nendobj\n");
            var commands = "q 288 0 0 432 0 0 cm /Im0 Do Q\n";
            Object(5, "<< /Length " + commands.Length + " >>\nstream\n" + commands + "endstream");
            long xref = buffer.Position;
            Write("xref\n0 6\n0000000000 65535 f \n");
            for (int i = 1; i <= 5; i++) Write(offsets[i].ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
            Write("trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n" + xref + "\n%%EOF\n");
            File.WriteAllBytes(path, buffer.ToArray());
        }
        public static void Print(BitmapSource image)
        {
            var dialog = new PrintDialog();
            dialog.PrintTicket.PageMediaSize = new System.Printing.PageMediaSize(Width, Height);
            dialog.PrintTicket.PageOrientation = System.Printing.PageOrientation.Portrait;
            if (dialog.ShowDialog() != true) return;
            var size = dialog.PrintTicket.PageMediaSize;
            if (size?.Width == null || size.Height == null || Math.Abs(size.Width.Value - Width) > 3 || Math.Abs(size.Height.Value - Height) > 3) throw new InvalidOperationException("请在打印机属性中选择 4×6 英寸纸张。");
            var page = new System.Windows.Documents.FixedPage { Width = Width, Height = Height };
            page.Children.Add(new Image { Source = image, Width = Width, Height = Height, Stretch = Stretch.Fill });
            page.Measure(new Size(Width, Height)); page.Arrange(new Rect(0, 0, Width, Height)); page.UpdateLayout();
            dialog.PrintVisual(page, "OMS 发货面单");
        }
        public static string PrintToPrinter(BitmapSource image, string printerName, string documentName)
        {
            using var server = new System.Printing.LocalPrintServer();
            using var queue = server.GetPrintQueue(printerName);
            queue.Refresh();
            if (queue.IsOffline || queue.IsInError) throw new InvalidOperationException("标签打印机离线或存在错误。");
            var ticket = (queue.UserPrintTicket ?? queue.DefaultPrintTicket).Clone();
            ticket.PageMediaSize = new System.Printing.PageMediaSize(Width, Height);
            ticket.PageOrientation = System.Printing.PageOrientation.Portrait;
            ticket.CopyCount = 1;
            var validated = queue.MergeAndValidatePrintTicket(queue.DefaultPrintTicket, ticket).ValidatedPrintTicket;
            var paper = validated.PageMediaSize;
            if (paper?.Width == null || paper.Height == null || Math.Abs(paper.Width.Value - Width) > 3 || Math.Abs(paper.Height.Value - Height) > 3)
                throw new InvalidOperationException("打印驱动未接受 4×6 英寸纸张，未发送打印任务。");
            var page = new System.Windows.Documents.FixedPage { Width = Width, Height = Height };
            page.Children.Add(new Image { Source = image, Width = Width, Height = Height, Stretch = Stretch.Fill });
            page.Measure(new Size(Width, Height)); page.Arrange(new Rect(0, 0, Width, Height)); page.UpdateLayout();
            queue.CurrentJobSettings.Description = documentName;
            var writer = System.Printing.PrintQueue.CreateXpsDocumentWriter(queue);
            writer.Write(page, validated);
            return documentName;
        }
    }
}
