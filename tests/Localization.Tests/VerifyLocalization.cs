using Scanner.WPF;
using Scanner.WPF.Helpers;
using Scanner.WPF.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;

internal static class VerifyLocalization
{
    [STAThread]
    private static int Main(string[] args)
    {
        try { return Run(args); }
        catch (Exception ex)
        {
            Console.WriteLine(ex.GetType().FullName + ": " + ex.Message);
            Console.WriteLine(ex.StackTrace);
            if (ex.InnerException != null) Console.WriteLine("Inner: " + ex.InnerException.Message);
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        string root = args[0];
        string output = args[1];
        Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources["Sentinel"] = "preserve";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        HashSet<string> expectedKeys = null;
        foreach (string language in new[] { "zh-CN", "en-US", "es-ES" })
        {
            var entries = XDocument.Load(Path.Combine(root, "SharedAssets", "Languages", "Language." + language + ".xaml")).Root.Elements().ToList();
            var keys = new HashSet<string>(entries.Select(e => (string)e.Attribute(x + "Key")));
            Assert(keys.Count == entries.Count, "Duplicate resources: " + language);
            if (expectedKeys == null) expectedKeys = keys;
            Assert(expectedKeys.SetEquals(keys), "Resource keys differ: " + language);
            UiText.ChangeLanguage(language);
            foreach (string key in keys) Assert(app.TryFindResource(key) != null, "Missing resource: " + key);
            Assert((string)app.Resources["Sentinel"] == "preserve", "Language switch removed unrelated resources");

            var assembly = typeof(MainWindow).Assembly;
            var provider = (IServiceProvider)assembly.GetType("Scanner.WPF.Controllers.DesktopComposition").GetMethod("Build", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
            var windows = new Window[] { (Window)provider.GetService(typeof(LoginWindow)), (Window)provider.GetService(typeof(MainWindow)), (Window)provider.GetService(typeof(ChecklistWindow)), (Window)provider.GetService(typeof(OutboundInspectionWindow)), (Window)provider.GetService(typeof(LocationFeeComparisonWindow)), new MeterModelSelectionWindow(new MeterModelService()) };
            foreach (Window window in windows)
            {
                Assert(window.FontFamily.Source == ((FontFamily)app.FindResource("AppFontFamily")).Source, "Window font mismatch: " + window.GetType().Name);
                Render(window, Path.Combine(output, window.GetType().Name + "-" + language + ".png"));
            }
            ((IDisposable)provider).Dispose();
        }
        UiText.ChangeLanguage("en-US");
        VerifyXaml(root, expectedKeys);
        var family = (FontFamily)app.FindResource("AppFontFamily");
        var primary = new FontFamily(family.BaseUri, family.Source.Replace(", Microsoft YaHei UI", ""));
        GlyphTypeface glyph;
        Assert(new Typeface(primary, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal).TryGetGlyphTypeface(out glyph), "Embedded font did not resolve");
        Assert(Uri.UnescapeDataString(glyph.FontUri.ToString()).Contains("Bickham Script Pro Semibold.ttf"), "Wrong font face: " + glyph.FontUri);
        Assert(glyph.Weight == FontWeights.SemiBold, "Wrong font weight");
        Console.WriteLine("PASS: " + expectedKeys.Count + " resource keys, 6 windows in 3 languages, embedded Semibold font.");
        return 0;
    }

    private static void Render(Window window, string path)
    {
        var content = (FrameworkElement)window.Content;
        int width = (int)window.Width - 16;
        int height = (int)window.Height - 39;
        content.Measure(new Size(width, height));
        content.Arrange(new Rect(0, 0, width, height));
        content.UpdateLayout();
        content.Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.Render);
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawRectangle(window.Background, null, new Rect(0, 0, width, height));
            dc.DrawRectangle(new VisualBrush(content) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, null, new Rect(0, 0, width, height));
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (Stream file = File.Create(path)) encoder.Save(file);
    }

    private static void VerifyXaml(string root, HashSet<string> keys)
    {
        var textProperties = new HashSet<string> { "Text", "Content", "Title", "Header", "ToolTip", "Placeholder", "AutomationProperties.Name" };
        var literals = new HashSet<string> { "S", "SN", "4 × 6", "4 × 4", "中文", "中", "EN", "ES", "English", "Español" };
        foreach (string project in new[] { "Scanner.WPF", "Scan.MaUI" })
        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, project), "*.xaml", SearchOption.AllDirectories))
        {
            string normalized = file.Replace('\\', '/');
            if (normalized.Contains("/obj/") || normalized.Contains("/bin/") || normalized.Contains("/Languages/")) continue;
            foreach (XAttribute attribute in XDocument.Load(file).Descendants().Attributes().Where(a => textProperties.Contains(a.Name.LocalName)))
            {
                string value = attribute.Value;
                if (value.StartsWith("{DynamicResource "))
                    Assert(keys.Contains(value.Substring(17).TrimEnd('}').Trim()), "Missing text resource: " + file + " " + value);
                else if (!value.StartsWith("{") && !string.IsNullOrWhiteSpace(value))
                    Assert(literals.Contains(value), "Untranslated XAML text: " + file + " " + value);
            }
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
