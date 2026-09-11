using Scanner.Models;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Documents;
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
        private static readonly WpfFontFamily PrintLatinAndNumberFont = new WpfFontFamily("/Scanner;component/Resources/Fonts/OpenSans-Regular.ttf#Open Sans");
        private static readonly WpfFontFamily PrintChineseFont = new WpfFontFamily("/Scanner;component/Resources/Fonts/IMing.ttf#I.Ming");
        public const string DefaultPaperSize = "4x6";
        public const string SquarePaperSize = "4x4";
        private const double WideLabelWidth = 576.0;
        private const double LabelHeight = 384.0;
        private const double SquareLabelWidth = 384.0;
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

        public void PrintLabel(string serialNumber, int copies, WorkOrderRemark workOrder, string meterModel)
        {
            PrintLabel(serialNumber, copies, workOrder, meterModel, DefaultPaperSize);
        }

        public void PrintLabel(string serialNumber, int copies, WorkOrderRemark workOrder, string meterModel, string paperSize)
        {
            if (string.IsNullOrWhiteSpace(serialNumber))
            {
                throw new ArgumentException("打印序列号不能为空。", nameof(serialNumber));
            }
            if (copies <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(copies), "打印份数必须大于零。");
            }
            string normalizedPaperSize = NormalizePaperSize(paperSize);
            Canvas label = CreateLabel(serialNumber.Trim(), workOrder, meterModel, normalizedPaperSize);
            BitmapSource source = Render(label, normalizedPaperSize);
            using (Bitmap bitmap = ToBitmap(source))
            {
                for (int i = 0; i < copies; i++)
                {
                    PrintBitmap(bitmap, normalizedPaperSize);
                }
            }
        }

        public void PrintOutboundInspection(string palletNumber, IList<OutboundSkuSummary> skuItems)
        {
            string pallet = (palletNumber ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(pallet)) throw new ArgumentException("托盘号不能为空。", nameof(palletNumber));
            if (skuItems == null || skuItems.Count == 0) throw new ArgumentException("没有可打印的 SKU 统计数据。", nameof(skuItems));

            const int itemsPerPage = 24;
            int pageCount = (skuItems.Count + itemsPerPage - 1) / itemsPerPage;
            int totalQuantity = skuItems.Sum(item => item.Quantity);
            for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                IList<OutboundSkuSummary> pageItems = skuItems.Skip(pageIndex * itemsPerPage).Take(itemsPerPage).ToList();
                Canvas label = CreateOutboundInspectionLabel(pallet, pageItems, totalQuantity, skuItems.Count, pageIndex + 1, pageCount);
                BitmapSource source = RenderOutboundLabel(label);
                using (Bitmap bitmap = ToBitmap(source)) PrintOutboundBitmap(bitmap);
            }
        }

        private static Canvas CreateOutboundInspectionLabel(string palletNumber, IList<OutboundSkuSummary> items, int totalQuantity, int skuCount, int pageNumber, int pageCount)
        {
            var canvas = new Canvas { Width = SquareLabelWidth, Height = WideLabelWidth, Background = WpfBrushes.White };
            AddText(canvas, Shorten(palletNumber, 32), 27, 18, 18, SquareLabelWidth - 36);
            AddText(canvas, string.Format("总数量：{0}    SKU种类：{1}", totalQuantity, skuCount), 15, 18, 58, SquareLabelWidth - 36);
            AddText(canvas, string.Format("第 {0}/{1} 页", pageNumber, pageCount), 12, 18, 82, SquareLabelWidth - 36);

            for (int index = 0; index < items.Count; index++)
            {
                OutboundSkuSummary item = items[index];
                string line = Shorten(item.Sku, 30) + "    × " + item.Quantity;
                AddText(canvas, line, 14, 18, 108 + index * 18, SquareLabelWidth - 36);
            }
            AddText(canvas, "打印时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm"), 11, 18, 554, SquareLabelWidth - 36);
            return canvas;
        }

        private static BitmapSource RenderOutboundLabel(Canvas canvas)
        {
            canvas.Measure(new System.Windows.Size(canvas.Width, canvas.Height));
            canvas.Arrange(new Rect(0, 0, canvas.Width, canvas.Height));
            canvas.UpdateLayout();
            var bitmap = new RenderTargetBitmap(1200, 1800, 300, 300, PixelFormats.Pbgra32);
            bitmap.Render(canvas);
            bitmap.Freeze();
            return bitmap;
        }

        private static void PrintOutboundBitmap(Bitmap bitmap)
        {
            using (var document = new PrintDocument())
            {
                document.PrintController = new StandardPrintController();
                if (!document.PrinterSettings.IsValid) throw new InvalidOperationException("Windows 默认打印机无效或不可用。");
                document.DefaultPageSettings.PaperSize = new PaperSize("4x6 Portrait", 400, 600);
                document.DefaultPageSettings.Landscape = false;
                document.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
                document.OriginAtMargins = false;
                document.PrintPage += (sender, args) =>
                {
                    if (args.Graphics == null) throw new InvalidOperationException("无法创建打印绘图环境。");
                    args.Graphics.DrawImage(bitmap, new Rectangle(args.PageBounds.Left, args.PageBounds.Top, args.PageBounds.Width, args.PageBounds.Height));
                    args.HasMorePages = false;
                };
                document.Print();
            }
        }

        public static string NormalizePaperSize(string paperSize)
        {
            return string.Equals(paperSize, SquarePaperSize, StringComparison.OrdinalIgnoreCase) ? SquarePaperSize : DefaultPaperSize;
        }

        private static Canvas CreateLabel(string serialNumber, WorkOrderRemark workOrder, string meterModel, string paperSize)
        {
            return string.Equals(paperSize, SquarePaperSize, StringComparison.Ordinal) ? CreateSquareLabel(serialNumber, workOrder, meterModel) : CreateWideLabel(serialNumber, workOrder, meterModel);
        }

        private static Canvas CreateWideLabel(string serialNumber, WorkOrderRemark workOrder, string meterModel)
        {
            bool urgent = workOrder != null && workOrder.IsUrgent;
            bool repair = workOrder != null && workOrder.IsRepair;
            string remark = urgent ? Shorten(workOrder.Remark, 50) : string.Empty;
            var canvas = new Canvas
            {
                Width = WideLabelWidth,
                Height = LabelHeight,
                Background = WpfBrushes.White
            };
            AddText(canvas, "SN : " + serialNumber, 27, 35, 12, WideLabelWidth - 70);
            string tailText = LastFive(serialNumber);
            if (!string.IsNullOrWhiteSpace(meterModel))
            {
                tailText = meterModel + "  " + tailText;
            }
            AddText(canvas, tailText, string.IsNullOrWhiteSpace(meterModel) ? 52 : 40, 30, 62, string.IsNullOrWhiteSpace(meterModel) ? 280 : 390);
            AddImage(canvas, CreateCode(serialNumber, BarcodeFormat.QR_CODE, 190, 190), WideLabelWidth - 135, 52, 105, 105, Stretch.Uniform);
            if (urgent)
            {
                AddMarker(canvas, "紧急", 95, 118, 150, 46, 32);
            }
            if (repair)
            {
                AddMarker(canvas, "修", urgent ? 265 : 95, 118, 62, 46, 32);
            }
            AddImage(canvas, CreateCode(serialNumber, BarcodeFormat.CODE_128, 430, 85), 78, 170, 420, 68, Stretch.Fill);
            AddText(canvas, serialNumber, 17, 35, 238, WideLabelWidth - 70);
            if (urgent && !string.IsNullOrWhiteSpace(remark))
            {
                var remarkText = CreateText("备注：" + remark, 18, WideLabelWidth - 60);
                remarkText.Height = 58;
                remarkText.TextAlignment = TextAlignment.Left;
                remarkText.TextWrapping = TextWrapping.Wrap;
                Canvas.SetLeft(remarkText, 30);
                Canvas.SetTop(remarkText, 267);
                canvas.Children.Add(remarkText);
            }
            AddText(canvas, "Date : " + DateTime.Now.ToString("yyyy-MM-dd"), urgent ? 20 : 25, 35, urgent ? 348 : 310, WideLabelWidth - 70);
            return canvas;
        }

        private static Canvas CreateSquareLabel(string serialNumber, WorkOrderRemark workOrder, string meterModel)
        {
            bool urgent = workOrder != null && workOrder.IsUrgent;
            bool repair = workOrder != null && workOrder.IsRepair;
            string remark = urgent ? Shorten(workOrder.Remark, 38) : string.Empty;
            var canvas = new Canvas
            {
                Width = SquareLabelWidth,
                Height = LabelHeight,
                Background = WpfBrushes.White
            };
            AddText(canvas, "SN : " + serialNumber, 20, 15, 8, SquareLabelWidth - 30);
            string tailText = LastFive(serialNumber);
            if (!string.IsNullOrWhiteSpace(meterModel))
            {
                tailText = meterModel + "  " + tailText;
            }
            AddText(canvas, tailText, string.IsNullOrWhiteSpace(meterModel) ? 40 : 31, 15, 48, 245);
            AddImage(canvas, CreateCode(serialNumber, BarcodeFormat.QR_CODE, 170, 170), SquareLabelWidth - 100, 43, 85, 85, Stretch.Uniform);
            if (urgent)
            {
                AddMarker(canvas, "紧急", 35, 101, 110, 40, 26);
            }
            if (repair)
            {
                AddMarker(canvas, "修", urgent ? 157 : 35, 101, 52, 40, 26);
            }
            AddImage(canvas, CreateCode(serialNumber, BarcodeFormat.CODE_128, 360, 80), 30, 151, 324, 60, Stretch.Fill);
            AddText(canvas, serialNumber, 14, 15, 211, SquareLabelWidth - 30);
            if (urgent && !string.IsNullOrWhiteSpace(remark))
            {
                var remarkText = CreateText("备注：" + remark, 15, SquareLabelWidth - 30);
                remarkText.Height = 84;
                remarkText.TextAlignment = TextAlignment.Left;
                remarkText.TextWrapping = TextWrapping.Wrap;
                Canvas.SetLeft(remarkText, 15);
                Canvas.SetTop(remarkText, 238);
                canvas.Children.Add(remarkText);
            }
            AddText(canvas, "Date : " + DateTime.Now.ToString("yyyy-MM-dd"), urgent ? 17 : 21, 15, urgent ? 352 : 320, SquareLabelWidth - 30);
            return canvas;
        }

        private static void AddMarker(Canvas canvas, string text, double left, double top, double width, double height, double fontSize)
        {
            TextBlock markerText = CreateText(text, fontSize, width);
            markerText.VerticalAlignment = VerticalAlignment.Center;
            var border = new Border
            {
                Width = width,
                Height = height,
                BorderBrush = WpfBrushes.Black,
                BorderThickness = new Thickness(3),
                Background = WpfBrushes.White,
                Child = markerText
            };
            Canvas.SetLeft(border, left);
            Canvas.SetTop(border, top);
            canvas.Children.Add(border);
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
            var block = new TextBlock
            {
                Width = width,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                FontFamily = PrintLatinAndNumberFont,
                Foreground = WpfBrushes.Black,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap
            };
            AddFontRuns(block, text ?? string.Empty);
            return block;
        }

        private static void AddFontRuns(TextBlock block, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            int start = 0;
            bool currentIsChinese = IsChineseCharacter(text[0]);
            for (int index = 1; index <= text.Length; index++)
            {
                bool boundary = index == text.Length || IsChineseCharacter(text[index]) != currentIsChinese;
                if (!boundary) continue;
                block.Inlines.Add(new Run(text.Substring(start, index - start))
                {
                    FontFamily = currentIsChinese ? PrintChineseFont : PrintLatinAndNumberFont
                });
                if (index < text.Length)
                {
                    start = index;
                    currentIsChinese = IsChineseCharacter(text[index]);
                }
            }
        }

        private static bool IsChineseCharacter(char character)
        {
            return char.IsDigit(character) || (character >= '\u3000' && character <= '\u303F') ||
                   (character >= '\u3400' && character <= '\u4DBF') ||
                   (character >= '\u4E00' && character <= '\u9FFF') ||
                   (character >= '\uF900' && character <= '\uFAFF') ||
                   (character >= '\uFF00' && character <= '\uFFEF');
        }

        private static void AddImage(Canvas canvas, BitmapSource source, double left, double top, double width, double height, Stretch stretch)
        {
            var image = new WpfImage
            {
                Source = source,
                Width = width,
                Height = height,
                Stretch = stretch
            };
            Canvas.SetLeft(image, left);
            Canvas.SetTop(image, top);
            canvas.Children.Add(image);
        }

        private static BitmapSource CreateCode(string content, BarcodeFormat format, int width, int height)
        {
            EncodingOptions options = format == BarcodeFormat.QR_CODE ? (EncodingOptions)new QrCodeEncodingOptions
            {
                Width = width,
                Height = height,
                Margin = 1,
                CharacterSet = "UTF-8"
            }
            : new EncodingOptions
            {
                Width = width,
                Height = height,
                Margin = 2,
                PureBarcode = true
            };
            var writer = new BarcodeWriter
            {
                Format = format,
                Options = options
            };
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

        private static BitmapSource Render(Canvas canvas, string paperSize)
        {
            canvas.Measure(new System.Windows.Size(canvas.Width, canvas.Height));
            canvas.Arrange(new Rect(0, 0, canvas.Width, canvas.Height));
            canvas.UpdateLayout();
            int pixelWidth = string.Equals(paperSize, SquarePaperSize, StringComparison.Ordinal) ? 1200 : 1800;
            var bitmap = new RenderTargetBitmap(pixelWidth, 1200, 300, 300, PixelFormats.Pbgra32);
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

        private static void PrintBitmap(Bitmap bitmap, string paperSize)
        {
            using (var document = new PrintDocument())
            {
                document.PrintController = new StandardPrintController();
                if (!document.PrinterSettings.IsValid)
                {
                    throw new InvalidOperationException("Windows 默认打印机无效或不可用。");
                }
                bool square = string.Equals(paperSize, SquarePaperSize, StringComparison.Ordinal);
                document.DefaultPageSettings.PaperSize = square ? new PaperSize("4x4", 400, 400) : new PaperSize("4x6", 400, 600);
                document.DefaultPageSettings.Landscape = !square;
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
            string clean = string.Join(" ", value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
            return clean.Length <= maxLength ? clean : clean.Substring(0, maxLength) + "…";
        }
    }
}
