using Scanner.Models;
using Scanner.WPF.Helpers;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Drawing.Printing;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var helper = new PrintingHelper();
        using var settings = new PrintDocument();
        Console.WriteLine("Default printer: " + settings.PrinterSettings.PrinterName + "; valid=" + settings.PrinterSettings.IsValid);
        foreach (string printer in PrinterSettings.InstalledPrinters) Console.WriteLine("Printer: " + printer);
        var order = new WorkOrderRemark { IsRepair = true };
        foreach (string size in new[] { PrintingHelper.DefaultPaperSize, PrintingHelper.SquarePaperSize })
        {
            var canvas = (Canvas)typeof(PrintingHelper).GetMethod("CreateLabel", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { "TEST-REPAIR-00001", order, "TEST", size, "", false, false })!;
            var source = (BitmapSource)typeof(PrintingHelper).GetMethod("Render", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { canvas, size })!;
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source));
            using var stream = System.IO.File.Create("D:/GitRepos/ScannerProject/tmp/repair-marker-preview/repair-" + size + ".png"); encoder.Save(stream);
            Console.WriteLine("Preview saved: " + size);
        }
        if (args.Contains("--print"))
        {
            if (!settings.PrinterSettings.IsValid) throw new Exception("No valid default printer");
            helper.PrintLabel("TEST-REPAIR-00001", 1, order, "TEST", PrintingHelper.DefaultPaperSize);
            Console.WriteLine("Sent ONE repair test label to " + settings.PrinterSettings.PrinterName);
        }
    }
}
