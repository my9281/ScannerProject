using Scanner.Models;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using DrawingImage = System.Drawing.Image;
using WpfImage = System.Windows.Controls.Image;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfFontFamily = System.Windows.Media.FontFamily;

namespace Scanner.Helpers
{
    public sealed class PrintingHelper
    {
        private const double LabelWidth = 576.0;
        private const double LabelHeight = 384.0;

        public string GetDefaultPrinterName()
        {
            using (var server = new LocalPrintServer())
            {
                PrintQueue queue = server.DefaultPrintQueue;
                if (queue == null)
                {
                    throw new InvalidOperationException("没有找到 Windows 默认打印机。");
                }

                return queue.FullName;
            }
        }

        public void PrintLabel(string serialNumber, int copies, WorkOrderRemark workOrder)
        {
            if (string.IsNullOrWhiteSpace(serialNumber))
            {
                throw new ArgumentException("打印序列号不能为空。", nameof(serialNumber));
            }

            if (copies <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(copies), "打印份数必须大于零。");
            }

            BitmapSource source = Render(CreateLabel(serialNumber.Trim(), workOrder));
            using (Bitmap bitmap = ToBitmap(source))
            {
                for (int i = 0; i < copies; i++)
                {
                    PrintBitmap(bitmap);
                }
            }
        }

        private static Canvas CreateLabel(string serialNumber, WorkOrderRemark workOrder)
        {
            bool urgent = workOrder != null && workOrder.IsUrgent;
            string remark = urgent ? Shorten(workOrder.Remark, 50) : string.Empty;
            var canvas = new Canvas { Width = LabelWidth, Height = LabelHeight, Background = WpfBrushes.White };

            AddText(canvas, "SN : " + serialNumber, 27, 35, 12, LabelWidth - 70);
            AddText(canvas, LastFive(serialNumber), 52, 30, 62, 280);
            AddImage(canvas, CreateCode(serialNumber, BarcodeFormat.QR_CODE, 190, 190), LabelWidth - 135, 52, 105, 105, Stretch.Uniform);

            if (urgent)
            {
                var urgentText = new TextBlock
                {
                    Text = "紧急",
                    FontSize = 32,
                    FontWeight = FontWeights.Bold,
                    FontFamily = new WpfFontFamily("Microsoft YaHei UI"),
                    Foreground = WpfBrushes.Black,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var border = new Border
                {
                    Width = 150,
                    Height = 46,
                    BorderBrush = WpfBrushes.Black,
                    BorderThickness = new Thickness(3),
                    Background = WpfBrushes.White,
                    Child = urgentText
                };
                Canvas.SetLeft(border, 95);
                Canvas.SetTop(border, 118);
                canvas.Children.Add(border);
            }

            AddImage(canvas, CreateCode(serialNumber, BarcodeFormat.CODE_128, 430, 85), 78, 170, 420, 68, Stretch.Fill);
            AddText(canvas, serialNumber, 17, 35, 238, LabelWidth - 70);
            if (urgent && !string.IsNullOrWhiteSpace(remark))
            {
                var remarkText = CreateText("备注：" + remark, 18, LabelWidth - 60);
                remarkText.Height = 58;
                remarkText.TextAlignment = TextAlignment.Left;
                remarkText.TextWrapping = TextWrapping.Wrap;
                Canvas.SetLeft(remarkText, 30);
                Canvas.SetTop(remarkText, 267);
                canvas.Children.Add(remarkText);
            }

            AddText(canvas, "Date : " + DateTime.Now.ToString("yyyy-MM-dd"), urgent ? 20 : 25, 35, urgent ? 348 : 310, LabelWidth - 70);
            return canvas;
        }

        private static void AddText(Canvas canvas, string text, double fontSize, double left, double top, double width)
        {
            TextBlock block = CreateText(text, fontSize, width);
            Canvas.SetLeft(block, left);
            Canvas.SetTop(block, top);
            canvas.Children.Add(block);
        }

        private static TextBlock CreateText(string text, double fontSize, double width)
        {
            return new TextBlock
            {
                Text = text,
                Width = width,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                FontFamily = new WpfFontFamily("Microsoft YaHei UI"),
                Foreground = WpfBrushes.Black,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap
            };
        }

        private static void AddImage(Canvas canvas, BitmapSource source, double left, double top, double width, double height, Stretch stretch)
        {
            var image = new WpfImage { Source = source, Width = width, Height = height, Stretch = stretch };
            Canvas.SetLeft(image, left);
            Canvas.SetTop(image, top);
            canvas.Children.Add(image);
        }

        private static BitmapSource CreateCode(string content, BarcodeFormat format, int width, int height)
        {
            EncodingOptions options = format == BarcodeFormat.QR_CODE
                ? (EncodingOptions)new QrCodeEncodingOptions { Width = width, Height = height, Margin = 1, CharacterSet = "UTF-8" }
                : new EncodingOptions { Width = width, Height = height, Margin = 2, PureBarcode = true };
            var writer = new BarcodeWriter { Format = format, Options = options };
            using (Bitmap bitmap = writer.Write(content))
            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png);
                stream.Position = 0;
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
        }

        private static BitmapSource Render(Canvas canvas)
        {
            canvas.Measure(new System.Windows.Size(LabelWidth, LabelHeight));
            canvas.Arrange(new Rect(0, 0, LabelWidth, LabelHeight));
            canvas.UpdateLayout();
            var bitmap = new RenderTargetBitmap(1800, 1200, 300, 300, PixelFormats.Pbgra32);
            bitmap.Render(canvas);
            bitmap.Freeze();
            return bitmap;
        }

        private static Bitmap ToBitmap(BitmapSource source)
        {
            using (var stream = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(source));
                encoder.Save(stream);
                stream.Position = 0;
                using (var temporary = new Bitmap(stream))
                {
                    return new Bitmap(temporary);
                }
            }
        }

        private static void PrintBitmap(Bitmap bitmap)
        {
            using (var document = new PrintDocument())
            {
                document.PrintController = new StandardPrintController();
                if (!document.PrinterSettings.IsValid)
                {
                    throw new InvalidOperationException("Windows 默认打印机无效或不可用。");
                }

                document.DefaultPageSettings.PaperSize = new PaperSize("4x6", 400, 600);
                document.DefaultPageSettings.Landscape = true;
                document.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
                document.OriginAtMargins = false;
                document.PrintPage += (sender, args) =>
                {
                    if (args.Graphics == null)
                    {
                        throw new InvalidOperationException("无法创建打印绘图环境。");
                    }

                    args.Graphics.DrawImage(bitmap, new Rectangle(args.PageBounds.Left, args.PageBounds.Top, args.PageBounds.Width, args.PageBounds.Height));
                    args.HasMorePages = false;
                };
                document.Print();
            }
        }

        private static string LastFive(string value)
        {
            return value.Length <= 5 ? value : value.Substring(value.Length - 5);
        }

        private static string Shorten(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string clean = value.Replace("\r", " ").Replace("\n", " ").Trim();
            return clean.Length <= maxLength ? clean : clean.Substring(0, maxLength) + "…";
        }
    }
}
