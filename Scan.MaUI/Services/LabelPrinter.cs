using Scanner.Models.MauiContracts;

#if WINDOWS
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.Reflection;
using ZXing;
using ZXing.Common;
using ZXing.Rendering;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;
using DrawingPointF = System.Drawing.PointF;
using DrawingSizeF = System.Drawing.SizeF;
#endif
#if MACCATALYST
using CoreGraphics;
using Foundation;
using UIKit;
using ZXing;
using ZXing.Common;
#endif

namespace Scan.MaUI.Services;

public interface ILabelPrinter
{
    bool IsSupported { get; }
    string GetDefaultPrinterName();
    Task PrintAsync(string serialNumber, int copies, WorkOrderRemark? workOrder, string meterModel, string paperSize);
}

public sealed class LabelPrinter : ILabelPrinter
{
#if WINDOWS
    private static readonly object FontSync = new();
    private static PrivateFontCollection? _printFonts;
    private static System.Drawing.FontFamily? _imingFamily;
    private static System.Drawing.FontFamily? _shouJinFamily;
#endif
    public bool IsSupported
    {
        get
        {
#if WINDOWS || MACCATALYST
            return true;
#else
            return false;
#endif
        }
    }
    public string GetDefaultPrinterName()
    {
#if WINDOWS
        using PrintDocument document = new();
        if (!document.PrinterSettings.IsValid) throw new InvalidOperationException("Windows 默认打印机无效或不可用。");
        return document.PrinterSettings.PrinterName;
#elif MACCATALYST
        return "macOS 系统打印机";
#else
        return "当前平台不支持自动标签打印";
#endif
    }
    public Task PrintAsync(string serialNumber, int copies, WorkOrderRemark? workOrder, string meterModel, string paperSize)
    {
#if WINDOWS
        return Task.Run(() => { EnsureWindowsPrintFonts(); PrintWindows(serialNumber, copies, workOrder, meterModel, paperSize); });
#elif MACCATALYST
        return PrintMacAsync(serialNumber, copies, workOrder, meterModel, paperSize);
#else
        throw new PlatformNotSupportedException("自动标签打印当前仅支持 Windows。	");
#endif
    }
#if WINDOWS
    private static void PrintWindows(string serialNumber, int copies, WorkOrderRemark? workOrder, string meterModel, string paperSize)
    {
        if (string.IsNullOrWhiteSpace(serialNumber)) throw new ArgumentException("打印编号不能为空。");
        using Bitmap label = CreateLabel(serialNumber.Trim(), workOrder, meterModel);
        for (int index = 0; index < copies; index++)
        {
            using PrintDocument document = new();
            document.PrintController = new StandardPrintController();
            if (!document.PrinterSettings.IsValid) throw new InvalidOperationException("Windows 默认打印机无效或不可用。");
            bool square = paperSize.Contains("4 × 4", StringComparison.Ordinal);
            document.DefaultPageSettings.PaperSize = new PaperSize(square ? "4x4" : "4x6", 400, square ? 400 : 600);
            document.DefaultPageSettings.Landscape = true;
            document.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
            document.PrintPage += (_, args) => args.Graphics?.DrawImage(label, args.PageBounds);
            document.Print();
        }
    }
    private static Bitmap CreateLabel(string serialNumber, WorkOrderRemark? workOrder, string meterModel)
    {
        Bitmap bitmap = new(1800, 1200, PixelFormat.Format32bppArgb);
        bitmap.SetResolution(300, 300);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(DrawingColor.White);
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        DrawMixedCentered(graphics, "SN : " + serialNumber, 27, new RectangleF(100, 35, 1600, 100));
        string tail = serialNumber.Length > 5 ? serialNumber[^5..] : serialNumber;
        string featured = string.IsNullOrWhiteSpace(meterModel) ? tail : meterModel + "  " + tail;
        DrawMixedCentered(graphics, featured, string.IsNullOrWhiteSpace(meterModel) ? 52 : 40, new RectangleF(80, 175, 1220, 160));
        using Bitmap qr = CreateBarcode(serialNumber, BarcodeFormat.QR_CODE, 360, 360);
        graphics.DrawImage(qr, 1370, 120, 320, 320);
        if (workOrder?.IsUrgent == true)
        {
            graphics.DrawRectangle(new Pen(DrawingColor.Black, 8), 285, 355, 450, 130);
            DrawMixedCentered(graphics, "紧急", 30, new RectangleF(285, 365, 450, 110));
        }
        using Bitmap barcode = CreateBarcode(serialNumber, BarcodeFormat.CODE_128, 1320, 220);
        graphics.DrawImage(barcode, 240, 510, 1320, 220);
        DrawMixedCentered(graphics, serialNumber, 17, new RectangleF(100, 745, 1600, 80));
        if (workOrder?.IsUrgent == true && !string.IsNullOrWhiteSpace(workOrder.Remark))
        {
            string remark = workOrder.Remark.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (remark.Length > 50) remark = remark[..50] + "…";
            DrawMixedLeft(graphics, "备注：" + remark, 18, new RectangleF(100, 825, 1600, 170));
        }
        DrawMixedCentered(graphics, "Date : " + DateTime.Now.ToString("yyyy-MM-dd"), workOrder?.IsUrgent == true ? 20 : 25, new RectangleF(100, workOrder?.IsUrgent == true ? 1060 : 950, 1600, 100));
        return bitmap;
    }

    private static void EnsureWindowsPrintFonts()
    {
        if (_printFonts != null) return;
        lock (FontSync)
        {
            if (_printFonts != null) return;
            string fontDirectory = Path.Combine(FileSystem.CacheDirectory, "print-fonts");
            Directory.CreateDirectory(fontDirectory);
            string imingPath = ExtractFont("Scan.MaUI.PrintFonts.IMing.ttf", Path.Combine(fontDirectory, "IMing.ttf"));
            string shouJinPath = ExtractFont("Scan.MaUI.PrintFonts.OpenSans-Regular.ttf", Path.Combine(fontDirectory, "OpenSans-Regular.ttf"));
            var collection = new PrivateFontCollection();
            collection.AddFontFile(imingPath);
            collection.AddFontFile(shouJinPath);
            _imingFamily = collection.Families.FirstOrDefault(item => item.Name.IndexOf("I.Ming", StringComparison.OrdinalIgnoreCase) >= 0)
                ?? throw new InvalidOperationException("无法加载打印字体 I.Ming。");
            _shouJinFamily = collection.Families.FirstOrDefault(item => item.Name.Equals("Open Sans", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("无法加载打印字体 Open Sans。");
            _printFonts = collection;
        }
    }

    private static string ExtractFont(string resourceName, string targetPath)
    {
        using Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("缺少打印字体资源：" + resourceName);
        using FileStream destination = new(targetPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        source.CopyTo(destination);
        return targetPath;
    }

    private static DrawingFont CreatePrintFont(float size, bool chinese)
    {
        EnsureWindowsPrintFonts();
        return new DrawingFont(chinese ? _imingFamily! : _shouJinFamily!, size, FontStyle.Regular, GraphicsUnit.Point);
    }

    private static void DrawMixedCentered(Graphics graphics, string text, float size, RectangleF bounds)
    {
        DrawMixed(graphics, text, size, bounds, true);
    }

    private static void DrawMixedLeft(Graphics graphics, string text, float size, RectangleF bounds)
    {
        DrawMixed(graphics, text, size, bounds, false);
    }

    private static void DrawMixed(Graphics graphics, string text, float size, RectangleF bounds, bool centered)
    {
        List<(string Text, bool Chinese)> runs = SplitFontRuns(text);
        using StringFormat format = new(StringFormat.GenericTypographic) { FormatFlags = StringFormatFlags.MeasureTrailingSpaces };
        var measured = new List<(string Text, DrawingFont Font, DrawingSizeF Size)>();
        float totalWidth = 0;
        float maximumHeight = 0;
        try
        {
            foreach ((string runText, bool chinese) in runs)
            {
                DrawingFont font = CreatePrintFont(size, chinese);
                DrawingSizeF measuredSize = graphics.MeasureString(runText, font, int.MaxValue, format);
                measured.Add((runText, font, measuredSize));
                totalWidth += measuredSize.Width;
                maximumHeight = Math.Max(maximumHeight, measuredSize.Height);
            }
            float x = centered ? bounds.X + Math.Max(0, (bounds.Width - totalWidth) / 2) : bounds.X;
            float y = bounds.Y + Math.Max(0, (bounds.Height - maximumHeight) / 2);
            foreach ((string runText, DrawingFont font, DrawingSizeF measuredSize) in measured)
            {
                graphics.DrawString(runText, font, Brushes.Black, new DrawingPointF(x, y), format);
                x += measuredSize.Width;
            }
        }
        finally
        {
            foreach (var item in measured) item.Font.Dispose();
        }
    }

    private static Bitmap CreateBarcode(string content, BarcodeFormat format, int width, int height)
    {
        BarcodeWriterPixelData writer = new() { Format = format, Options = new EncodingOptions { Width = width, Height = height, Margin = format == BarcodeFormat.QR_CODE ? 1 : 2, PureBarcode = format != BarcodeFormat.QR_CODE } };
        PixelData pixels = writer.Write(content);
        Bitmap bitmap = new(pixels.Width, pixels.Height, PixelFormat.Format32bppRgb);
        BitmapData data = bitmap.LockBits(new Rectangle(0, 0, pixels.Width, pixels.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
        System.Runtime.InteropServices.Marshal.Copy(pixels.Pixels, 0, data.Scan0, pixels.Pixels.Length);
        bitmap.UnlockBits(data);
        return bitmap;
    }
#endif

#if MACCATALYST
    private static async Task PrintMacAsync(string serialNumber, int copies, WorkOrderRemark? workOrder, string meterModel, string paperSize)
    {
        string pdf = CreateMacLabelPdf(serialNumber, workOrder, meterModel, paperSize);
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            UIPrintInteractionController controller = UIPrintInteractionController.SharedPrintController;
            controller.PrintInfo = UIPrintInfo.PrintInfo;
            controller.PrintInfo.JobName = $"Label {serialNumber}";
            controller.PrintInfo.OutputType = UIPrintInfoOutputType.General;
            controller.PrintingItem = NSData.FromFile(pdf);
            for (int index = 0; index < copies; index++) await controller.PresentAsync(true);
        });
    }

    private static string CreateMacLabelPdf(string serialNumber, WorkOrderRemark? workOrder, string meterModel, string paperSize)
    {
        string path = Path.Combine(FileSystem.CacheDirectory, $"label_{Guid.NewGuid():N}.pdf");
        CGRect page = paperSize.Contains("4 × 4", StringComparison.Ordinal) ? new CGRect(0, 0, 288, 288) : new CGRect(0, 0, 432, 288);
        UIGraphics.BeginPDFContext(path, page, (NSDictionary?)null);
        UIGraphics.BeginPDFPage();
        CGContext context = UIGraphics.GetCurrentContext();
        context.ScaleCTM((nfloat)(page.Width / 432d), 1);
        UIColor.Black.SetColor();
        DrawCentered($"SN : {serialNumber}", 18, 12, 300);
        string tail = serialNumber.Length > 5 ? serialNumber[^5..] : serialNumber;
        DrawCentered(string.IsNullOrWhiteSpace(meterModel) ? tail : $"{meterModel}  {tail}", 30, 42, 310);
        DrawCode(context, serialNumber, BarcodeFormat.QR_CODE, new CGRect(330, 12, 88, 88));
        if (workOrder?.IsUrgent == true) { context.SetLineWidth(3); context.StrokeRect(new CGRect(55, 88, 110, 35)); DrawCentered("紧急", 22, 93, 220); }
        DrawCode(context, serialNumber, BarcodeFormat.CODE_128, new CGRect(42, 132, 348, 62));
        DrawCentered(serialNumber, 13, 200, 432);
        if (workOrder?.IsUrgent == true && !string.IsNullOrWhiteSpace(workOrder.Remark)) DrawCentered("备注：" + workOrder.Remark.Replace('\r', ' ').Replace('\n', ' '), 11, 226, 432);
        DrawCentered("Date : " + DateTime.Now.ToString("yyyy-MM-dd"), 15, 260, 432);
        UIGraphics.EndPDFContext();
        return path;
    }

    private static void DrawCentered(string text, nfloat size, nfloat y, nfloat width)
    {
        List<(string Text, bool Chinese)> runs = SplitFontRuns(text);
        var measured = new List<(NSString Text, UIStringAttributes Attributes, CGSize Size)>();
        nfloat totalWidth = 0;
        foreach ((string runText, bool chinese) in runs)
        {
            UIFont font = GetMacPrintFont(size, chinese);
            var attributes = new UIStringAttributes { Font = font, ForegroundColor = UIColor.Black };
            var nativeText = new NSString(runText);
            CGSize measuredSize = nativeText.GetSizeUsingAttributes(attributes);
            measured.Add((nativeText, attributes, measuredSize));
            totalWidth += measuredSize.Width;
        }
        nfloat remainingWidth = width - 16 - totalWidth;
        nfloat x = 8 + (remainingWidth > 0 ? remainingWidth / 2 : 0);
        foreach (var run in measured)
        {
            run.Text.DrawString(new CGPoint(x, y), run.Attributes);
            x += run.Size.Width;
            run.Text.Dispose();
        }
    }

    private static UIFont GetMacPrintFont(nfloat size, bool chinese)
    {
        string[] names = chinese ? new[] { "I.Ming" } : new[] { "OpenSans-Regular" };
        foreach (string name in names)
        {
            UIFont? font = UIFont.FromName(name, size);
            if (font != null) return font;
        }
        throw new InvalidOperationException(chinese ? "无法加载打印字体 I.Ming。" : "无法加载打印字体 Open Sans。");
    }

    private static void DrawCode(CGContext context, string text, BarcodeFormat format, CGRect target)
    {
        var matrix = new MultiFormatWriter().encode(text, format, (int)target.Width, (int)target.Height, new Dictionary<EncodeHintType, object> { [EncodeHintType.MARGIN] = 1 });
        nfloat sx = target.Width / matrix.Width, sy = target.Height / matrix.Height;
        context.SetFillColor(UIColor.White.CGColor); context.FillRect(target); context.SetFillColor(UIColor.Black.CGColor);
        for (int y = 0; y < matrix.Height; y++) for (int x = 0; x < matrix.Width; x++) if (matrix[x, y]) context.FillRect(new CGRect(target.X + x * sx, target.Y + y * sy, sx + .2, sy + .2));
    }
#endif

    private static List<(string Text, bool Chinese)> SplitFontRuns(string text)
    {
        var result = new List<(string Text, bool Chinese)>();
        if (string.IsNullOrEmpty(text)) return result;
        int start = 0;
        bool currentIsChinese = IsChineseCharacter(text[0]);
        for (int index = 1; index <= text.Length; index++)
        {
            bool boundary = index == text.Length || IsChineseCharacter(text[index]) != currentIsChinese;
            if (!boundary) continue;
            result.Add((text.Substring(start, index - start), currentIsChinese));
            if (index < text.Length)
            {
                start = index;
                currentIsChinese = IsChineseCharacter(text[index]);
            }
        }
        return result;
    }

    private static bool IsChineseCharacter(char character)
    {
        return char.IsDigit(character) || (character >= '\u3000' && character <= '\u303F') ||
               (character >= '\u3400' && character <= '\u4DBF') ||
               (character >= '\u4E00' && character <= '\u9FFF') ||
               (character >= '\uF900' && character <= '\uFAFF') ||
               (character >= '\uFF00' && character <= '\uFFEF');
    }
}
