#if ANDROID
using Android.Graphics;
using Android.Graphics.Pdf;
using Android.OS;
using Android.Print;
using ZXing;
using ZXing.Common;
using Paint = Android.Graphics.Paint;
using Color = Android.Graphics.Color;

namespace Scanner.AndroidTester.Services;

public static class AndroidLabelPrinter
{
    public static Bitmap CreatePrinterBitmap(string sn)
    {
        var qr = new MultiFormatWriter().encode(sn, BarcodeFormat.QR_CODE, 190, 190);
        var barcode = new MultiFormatWriter().encode(sn, BarcodeFormat.CODE_128, 430, 85);
        var bitmap = Bitmap.CreateBitmap(816, 1216, Bitmap.Config.Argb8888!)!;
        try
        {
            using var canvas = new Canvas(bitmap);
            canvas.DrawColor(Color.White);
            canvas.Translate(816, 0);
            canvas.Rotate(90);
            canvas.Scale(1216f / 576, 816f / 384);
            using var adapter = new LabelAdapter(sn, qr, barcode, DateTime.Now);
            adapter.DrawLabel(canvas);
            return bitmap;
        }
        catch { bitmap.Dispose(); throw; }
    }
    public static void Print(string sn)
    {
        // Validate both encodings before opening the system print UI.
        var qr = new MultiFormatWriter().encode(sn, BarcodeFormat.QR_CODE, 190, 190);
        var barcode = new MultiFormatWriter().encode(sn, BarcodeFormat.CODE_128, 430, 85);
        var activity = Platform.CurrentActivity ?? throw new InvalidOperationException("当前活动不可用。");
        var manager = (PrintManager?)activity.GetSystemService(Android.Content.Context.PrintService)
            ?? throw new InvalidOperationException("设备未提供系统打印服务。");
        var attributes = new PrintAttributes.Builder()
            .SetMediaSize(new PrintAttributes.MediaSize("label-4x6", "4 × 6 标签", 6000, 4000))
            .SetMinMargins(new PrintAttributes.Margins(0, 0, 0, 0))
            .SetResolution(new PrintAttributes.Resolution("label300", "300 dpi", 300, 300))
            .SetColorMode(PrintColorMode.Monochrome).Build();
        manager.Print("SN 标签", new LabelAdapter(sn, qr, barcode, DateTime.Now), attributes);
    }

    private sealed class LabelAdapter(string sn, BitMatrix qr, BitMatrix barcode, DateTime printedAt) : PrintDocumentAdapter
    {
        private int _width = 432, _height = 288;
        public override void OnLayout(PrintAttributes? oldAttributes, PrintAttributes? newAttributes, CancellationSignal? cancellationSignal, LayoutResultCallback? callback, Bundle? extras)
        {
            if (cancellationSignal?.IsCanceled == true) { callback?.OnLayoutCancelled(); return; }
            if (newAttributes?.GetMediaSize() is { } size) { _width = size.WidthMils * 72 / 1000; _height = size.HeightMils * 72 / 1000; }
            callback?.OnLayoutFinished(new PrintDocumentInfo.Builder("SN-label.pdf").SetContentType(PrintContentType.Document).SetPageCount(1).Build(), true);
        }
        public override void OnWrite(PageRange[]? pages, ParcelFileDescriptor? destination, CancellationSignal? cancellationSignal, WriteResultCallback? callback)
        {
            try
            {
                if (cancellationSignal?.IsCanceled == true) { callback?.OnWriteCancelled(); return; }
                if (pages is null || !pages.Any(x => x.Start <= 0 && x.End >= 0)) { callback?.OnWriteFinished(Array.Empty<PageRange>()); return; }
                using var document = new PdfDocument();
                using var info = new PdfDocument.PageInfo.Builder(_width, _height, 1).Create();
                var page = document.StartPage(info) ?? throw new InvalidOperationException("无法创建标签页面。");
                var canvas = page.Canvas!;
                canvas.DrawColor(Color.White);
                float scale = Math.Min(_width / 576f, _height / 384f);
                canvas.Translate((_width - 576 * scale) / 2, (_height - 384 * scale) / 2);
                canvas.Scale(scale, scale);
                DrawLabel(canvas);
                document.FinishPage(page);
                if (cancellationSignal?.IsCanceled == true) { callback?.OnWriteCancelled(); return; }
                using var output = new Java.IO.FileOutputStream(destination!.FileDescriptor);
                using var memory = new MemoryStream();
                document.WriteTo(memory);
                output.Write(memory.ToArray());
                output.Flush();
                callback?.OnWriteFinished(new[] { new PageRange(0, 0) });
            }
            catch (Exception ex) { callback?.OnWriteFailed(ex.Message); }
        }
        internal void DrawLabel(Canvas canvas)
        {
            // WPF PrintingHelper.CreateWideLabel: identical 576 × 384 layout.
            Text(canvas, "SN : " + sn, 27, 35, 12, 506);
            Text(canvas, sn.Length > 5 ? sn[^5..] : sn, 52, 30, 62, 280);
            Code(canvas, qr, 441, 52, 105, 105);
            Code(canvas, barcode, 78, 170, 420, 68);
            Text(canvas, sn, 17, 35, 238, 506);
            Text(canvas, "Date : " + printedAt.ToString("yyyy-MM-dd"), 25, 35, 310, 506);
        }
        private static void Text(Canvas canvas, string value, float size, float x, float y, float width)
        {
            using var paint = new Paint(PaintFlags.AntiAlias) { Color = Color.Black, TextSize = size, TextAlign = Paint.Align.Center };
            float measured = paint.MeasureText(value);
            if (measured > width) paint.TextSize *= width / measured;
            canvas.DrawText(value, x + width / 2, y - paint.GetFontMetrics()!.Top, paint);
        }
        private static void Code(Canvas canvas, BitMatrix matrix, float x, float y, float width, float height)
        {
            using var paint = new Paint { Color = Color.Black, AntiAlias = false };
            float dx = width / matrix.Width, dy = height / matrix.Height;
            for (int row = 0; row < matrix.Height; row++)
                for (int col = 0; col < matrix.Width; col++)
                    if (matrix[col, row]) canvas.DrawRect(x + col * dx, y + row * dy, x + (col + 1) * dx, y + (row + 1) * dy, paint);
        }
    }
}
#endif
