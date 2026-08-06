using MaUIScanner.Models;

#if WINDOWS
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using ZXing;
using ZXing.Common;
using ZXing.Rendering;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;
#endif

namespace MaUIScanner.Services;

public interface ILabelPrinter
{
    string GetDefaultPrinterName();
    Task PrintAsync(string serialNumber, int copies, WorkOrderRemark? workOrder, string meterModel);
}

public sealed class LabelPrinter : ILabelPrinter
{
    public string GetDefaultPrinterName()
    {
#if WINDOWS
        using PrintDocument document = new();
        if (!document.PrinterSettings.IsValid) throw new InvalidOperationException("Windows 默认打印机无效或不可用。");
        return document.PrinterSettings.PrinterName;
#else
        return "当前平台不支持自动标签打印";
#endif
    }
    public Task PrintAsync(string serialNumber, int copies, WorkOrderRemark? workOrder, string meterModel)
    {
#if WINDOWS
        return Task.Run(() => PrintWindows(serialNumber, copies, workOrder, meterModel));
#else
        throw new PlatformNotSupportedException("自动标签打印当前仅支持 Windows。	");
#endif
    }
#if WINDOWS
    private static void PrintWindows(string serialNumber, int copies, WorkOrderRemark? workOrder, string meterModel)
    {
        if (string.IsNullOrWhiteSpace(serialNumber)) throw new ArgumentException("打印编号不能为空。");
        using Bitmap label = CreateLabel(serialNumber.Trim(), workOrder, meterModel);
        for (int index = 0; index < copies; index++)
        {
            using PrintDocument document = new();
            document.PrintController = new StandardPrintController();
            if (!document.PrinterSettings.IsValid) throw new InvalidOperationException("Windows 默认打印机无效或不可用。");
            document.DefaultPageSettings.PaperSize = new PaperSize("4x6", 400, 600);
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
        using DrawingFont titleFont = new("Microsoft YaHei UI", 27, FontStyle.Bold);
        using DrawingFont tailFont = new("Microsoft YaHei UI", string.IsNullOrWhiteSpace(meterModel) ? 52 : 40, FontStyle.Bold);
        using DrawingFont codeFont = new("Microsoft YaHei UI", 17, FontStyle.Bold);
        using DrawingFont dateFont = new("Microsoft YaHei UI", workOrder?.IsUrgent == true ? 20 : 25, FontStyle.Bold);
        using StringFormat center = new() { Alignment = StringAlignment.Center };
        graphics.DrawString("SN : " + serialNumber, titleFont, Brushes.Black, new RectangleF(100, 35, 1600, 100), center);
        string tail = serialNumber.Length > 5 ? serialNumber[^5..] : serialNumber;
        string featured = string.IsNullOrWhiteSpace(meterModel) ? tail : meterModel + "  " + tail;
        graphics.DrawString(featured, tailFont, Brushes.Black, new RectangleF(80, 175, 1220, 160), center);
        using Bitmap qr = CreateBarcode(serialNumber, BarcodeFormat.QR_CODE, 360, 360);
        graphics.DrawImage(qr, 1370, 120, 320, 320);
        if (workOrder?.IsUrgent == true)
        {
            using DrawingFont urgentFont = new("Microsoft YaHei UI", 30, FontStyle.Bold);
            graphics.DrawRectangle(new Pen(DrawingColor.Black, 8), 285, 355, 450, 130);
            graphics.DrawString("紧急", urgentFont, Brushes.Black, new RectangleF(285, 365, 450, 110), center);
        }
        using Bitmap barcode = CreateBarcode(serialNumber, BarcodeFormat.CODE_128, 1320, 220);
        graphics.DrawImage(barcode, 240, 510, 1320, 220);
        graphics.DrawString(serialNumber, codeFont, Brushes.Black, new RectangleF(100, 745, 1600, 80), center);
        if (workOrder?.IsUrgent == true && !string.IsNullOrWhiteSpace(workOrder.Remark))
        {
            using DrawingFont remarkFont = new("Microsoft YaHei UI", 18, FontStyle.Bold);
            string remark = workOrder.Remark.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (remark.Length > 50) remark = remark[..50] + "…";
            graphics.DrawString("备注：" + remark, remarkFont, Brushes.Black, new RectangleF(100, 825, 1600, 170));
        }
        graphics.DrawString("Date : " + DateTime.Now.ToString("yyyy-MM-dd"), dateFont, Brushes.Black, new RectangleF(100, workOrder?.IsUrgent == true ? 1060 : 950, 1600, 100), center);
        return bitmap;
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
}
