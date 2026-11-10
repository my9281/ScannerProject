using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Scanner.WPF.Services;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        int exit = 1;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) =>
        {
            try
            {
                if (args.Length == 6 && args[0] == "render")
                {
                    var image = await OmsLabelDocument.RenderAsync(args[1], args[3], args[4], args[5]);
                    OmsLabelDocument.SavePng(image, args[2] + ".png"); OmsLabelDocument.SavePdf(image, args[2] + ".pdf");
                    Console.WriteLine("RENDERED: " + args[2]);
                }
                else if (args.Length == 4 && args[0] == "print")
                {
                    // A receipt marker blocks accidental command reruns for this image.
                    string receipt = args[1] + ".print-receipt.txt";
                    using (var marker = new FileStream(receipt, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var writer = new StreamWriter(marker)) writer.Write("Print attempt started " + DateTimeOffset.Now.ToString("O"));
                    var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(Path.GetFullPath(args[1])); image.EndInit(); image.Freeze();
                    string job = OmsLabelDocument.PrintToPrinter(image, args[2], args[3]);
                    File.AppendAllText(receipt, Environment.NewLine + "Submitted to Windows spooler: " + args[2] + "; job=" + job + "; copies=1; paper=4x6");
                    Console.WriteLine("SUBMITTED: " + job + "; printer=" + args[2] + "; copies=1; paper=4x6");
                }
                else throw new ArgumentException("render <source.pdf> <output-prefix> <rma> <model> <sn> OR print <image.png> <printer> <job-name>");
                exit = 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); }
            finally { app.Shutdown(); }
        };
        app.Run(); return exit;
    }
}
