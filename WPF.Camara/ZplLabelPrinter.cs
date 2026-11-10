using System.IO;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WPF.Camara;

public static class ZplLabelPrinter
{
    // Keep each graphic small for ZPL-compatible firmware as well as the ZPL 99999-byte limit.
    public const int MaximumGraphicBytes = 4096;
    public static byte[] Encode(FrameworkElement label, int dpi)
    {
        if (dpi is not (203 or 300)) throw new ArgumentOutOfRangeException(nameof(dpi));
        label.Measure(new Size(label.Width, label.Height));
        label.Arrange(new Rect(0, 0, label.Width, label.Height));
        label.UpdateLayout();
        int width = (int)Math.Round(label.Width / 96 * dpi), height = (int)Math.Round(label.Height / 96 * dpi);
        var bitmap = new RenderTargetBitmap(width, height, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(label);
        var pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        int stride = (width + 7) / 8;
        var packed = new byte[stride * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int pos = (y * width + x) * 4;
            int gray = (pixels[pos] * 114 + pixels[pos + 1] * 587 + pixels[pos + 2] * 299) / 1000;
            if (gray + 255 - pixels[pos + 3] < 128) packed[y * stride + x / 8] |= (byte)(0x80 >> (x % 8));
        }
        // The whole 4x6 raster exceeds the permitted size of a single ^GF, even at 203 DPI.
        // Position independent, row-aligned strips in ONE format, so these still produce one label.
        int rowsPerStrip = Math.Max(1, MaximumGraphicBytes / stride);
        var zpl = new StringBuilder($"^XA^PW{width}^LL{height}^LH0,0");
        for (int y = 0; y < height; y += rowsPerStrip)
        {
            int bytes = Math.Min(rowsPerStrip, height - y) * stride;
            zpl.Append($"^FO0,{y}^GFA,{bytes},{bytes},{stride},");
            zpl.Append(Convert.ToHexString(packed.AsSpan(y * stride, bytes)));
            zpl.Append("^FS");
        }
        zpl.Append("^PQ1^XZ");
        return Encoding.ASCII.GetBytes(zpl.ToString());
    }

    public static void WriteInChunks(byte[] data, Func<byte[], int> write)
    {
        int offset = 0;
        while (offset < data.Length)
        {
            int count = Math.Min(4096, data.Length - offset);
            var chunk = data.AsSpan(offset, count).ToArray();
            int written = write(chunk);
            if (written <= 0 || written > count)
                throw new IOException($"条码数据发送不完整：已写 {offset}/{data.Length} 字节，当前写入返回 {written}。");
            // A successful short write resumes at the first unwritten byte, never resends an accepted prefix.
            offset += written;
        }
    }

    public static int Send(string printer, string title, byte[] data)
    {
        if (!OpenPrinter(printer, out var handle, IntPtr.Zero)) throw new Win32Exception();
        bool started = false;
        try
        {
            int jobId = StartDocPrinter(handle, 1, new DocInfo { Name = title, DataType = "RAW" });
            if (jobId == 0) throw new Win32Exception();
            started = true;
            if (!StartPagePrinter(handle)) throw new Win32Exception();
            WriteInChunks(data, chunk =>
            {
                if (!WritePrinter(handle, chunk, chunk.Length, out int written)) throw new Win32Exception();
                return written;
            });
            if (!EndPagePrinter(handle) || !EndDocPrinter(handle)) throw new Win32Exception();
            started = false;
            return jobId;
        }
        finally
        {
            if (started) AbortPrinter(handle);
            ClosePrinter(handle);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class DocInfo { [MarshalAs(UnmanagedType.LPWStr)] public string Name = ""; [MarshalAs(UnmanagedType.LPWStr)] public string? OutputFile; [MarshalAs(UnmanagedType.LPWStr)] public string DataType = "RAW"; }
    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)] private static extern bool OpenPrinter(string name, out IntPtr handle, IntPtr defaults);
    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)] private static extern int StartDocPrinter(IntPtr handle, int level, [In] DocInfo info);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool StartPagePrinter(IntPtr handle);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool WritePrinter(IntPtr handle, byte[] data, int count, out int written);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool EndPagePrinter(IntPtr handle);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool EndDocPrinter(IntPtr handle);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool AbortPrinter(IntPtr handle);
    [DllImport("winspool.drv")] private static extern bool ClosePrinter(IntPtr handle);
}

public static class LabelSendSequence
{
    public static async Task<int> RunAsync(int count, Func<int, Task> send, Action<int> progress, CancellationToken token)
    {
        int sent = 0;
        for (int i = 0; i < count; i++)
        {
            if (token.IsCancellationRequested) break;
            await send(i);
            progress(++sent);
        }
        return sent;
    }
}

