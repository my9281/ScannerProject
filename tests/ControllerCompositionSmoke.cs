using System;
using System.IO;
using System.Reflection;
using System.Windows;
class ControllerCompositionSmoke
{
    [STAThread]
    static void Main(string[] args)
    {
        var app = new Application();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        string dir = Path.GetFullPath(args[0]);
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {
            string path = Path.Combine(dir, new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        var assembly = Assembly.LoadFrom(Path.Combine(dir, "Scanner.WPF.exe"));
        var root = assembly.GetType("Scanner.WPF.Controllers.DesktopComposition").GetMethod("Build", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        try
        {
            foreach (string name in new[] { "LoginWindow", "MainWindow", "ChecklistWindow", "OutboundInspectionWindow", "LocationFeeComparisonWindow" })
            {
                Window window;
                try { window = (Window)((IServiceProvider)root).GetService(assembly.GetType("Scanner.WPF." + name)); }
                catch (Exception error) { for (var e = error; e != null; e = e.InnerException) Console.WriteLine(e.GetType().FullName + ": " + e.Message); Environment.ExitCode = 1; return; }
                if (window == null) throw new Exception("Missing window: " + name);
                window.Close();
                Console.WriteLine("PASS: DI factory created and closed " + name);
            }
        }
        finally { ((IDisposable)root).Dispose(); }
    }
}
