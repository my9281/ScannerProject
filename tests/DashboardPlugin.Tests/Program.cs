using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Scanner.WPF.Services;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
        string root = Path.Combine(Path.GetTempPath(), "DashboardPluginTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Plugins"));
        try
        {
            var app = new Application();
            app.Resources["DashboardCulture"] = "zh-CN";
            var version = new Version(8, 7, 6, 5);
            Check(DashboardPluginLoader.Load(root, version) == null, "Missing plugin is optional");
            string target = Path.Combine(root, "Plugins", "Scanner.DashboardPlugin.dll");
            File.WriteAllText(target, "broken DLL");
            Check(DashboardPluginLoader.Load(root, version) == null, "Broken DLL is isolated");
            File.Copy(typeof(Program).Assembly.Location, target, true);
            Check(DashboardPluginLoader.Load(root, version) == null, "Incompatible entry point is isolated");
            File.Copy(Path.GetFullPath(args[0]), target, true);
            var view = (UserControl)DashboardPluginLoader.Load(root, version);
            Check(view != null, "Runtime DLL and embedded XAML load successfully");
            Check(((TextBlock)view.FindName("VersionText")).Text == "v8.7.6.5", "Shows host version rather than plugin version");
            Check(!view.GetType().Assembly.GetReferencedAssemblies().Any(a => a.Name == "Scanner.WPF"), "Plugin is independent of host");
            var host = new Window { Content = view, Width = 500, Height = 160, Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false };
            host.Show();
            Check(((TextBlock)view.FindName("NjClock")).Text.Contains("新泽西"), "Clock starts on load");
            // Seed cached values, then switch the host resource without another network request.
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            void Set(string field, object value) => view.GetType().GetField(field, flags).SetValue(view, value);
            Set("sunDay", TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Eastern Standard Time").Date);
            Set("sunrise", new DateTime(2026, 10, 30, 7, 24, 0));
            Set("sunset", new DateTime(2026, 10, 30, 17, 55, 0));
            Set("observedAt", new DateTime(2026, 10, 30, 10, 18, 0));
            Set("temperature", 18.9); Set("weatherCode", 0); Set("hasWeather", true);
            view.GetType().GetMethod("RenderDashboard", flags).Invoke(view, null);
            foreach (var (language, title, clear) in new[] { ("zh-CN", "当前版本", "晴"), ("en-US", "Current version", "Clear"), ("es-ES", "Versión actual", "Despejado") })
            {
                app.Resources["DashboardCulture"] = language;
                view.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                string Text(string name) => ((TextBlock)view.FindName(name)).Text;
                Check((string)view.GetValue((DependencyProperty)view.GetType().GetField("UiCultureProperty").GetValue(null)) == language, "Host language resource updates plugin: " + language);
                Check(Text("VersionTitle") == title && Text("Weather").StartsWith(clear), "Labels and cached weather: " + language);
                Check(Text("Sun").Contains("07:24") && Text("Sun").Contains("17:55") && Text("Weather").Contains("66"), "Cached times and temperatures preserved: " + language);
                if (language != "zh-CN")
                    foreach (var name in new[] { "NjClock", "BeijingClock", "Zones", "SunTitle", "Sun", "SunDate", "WeatherTitle", "Weather", "WeatherTime", "VersionTitle", "VersionHint" })
                        Check(!Text(name).Any(c => c is >= '\u4e00' and <= '\u9fff'), "No Chinese in " + language + " " + name);
                foreach (int code in new[] { 0, 1, 2, 3, 45, 48, 51, 53, 55, 56, 57, 61, 63, 65, 66, 67, 71, 73, 75, 77, 80, 81, 82, 85, 86, 95, 96, 99, -1 })
                {
                    string description = (string)view.GetType().GetMethod("Describe", flags).Invoke(view, new object[] { code });
                    Check(!string.IsNullOrWhiteSpace(description) && (language == "zh-CN" || !description.Any(c => c is >= '\u4e00' and <= '\u9fff')), "Weather code " + code + ": " + language);
                }
                Set("refreshFailed", true);
                view.GetType().GetMethod("RenderDashboard", flags).Invoke(view, null);
                Check(Text("Weather").StartsWith(clear), "Offline retains cached data: " + language);
                Check(language == "zh-CN" || !Text("WeatherTime").Any(c => c is >= '\u4e00' and <= '\u9fff'), "Offline message localized: " + language);
                Set("refreshFailed", false);
            }
            view.Measure(new Size(500, double.PositiveInfinity));
            view.Arrange(new Rect(0, 0, 500, view.DesiredSize.Height));
            view.UpdateLayout();
            Check(view.ActualHeight > 0 && view.ActualHeight <= 122, "Fits existing header slot");
            if (args.Length > 1)
            {
                var bitmap = new RenderTargetBitmap(500, (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(view);
                var png = new PngBitmapEncoder();
                png.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(args[1]);
                png.Save(file);
            }
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            host.Close();
            var lifetime = view.GetType().GetField("lifetime", BindingFlags.NonPublic | BindingFlags.Instance);
            Check(lifetime.GetValue(view) == null, "Unloading cancels network lifetime");
        }
        finally
        {
            // Loaded assemblies stay mapped until process exit, so cleanup must tolerate Windows file locks.
            try { Directory.Delete(root, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        Console.WriteLine("PASS: " + label);
    }
}
