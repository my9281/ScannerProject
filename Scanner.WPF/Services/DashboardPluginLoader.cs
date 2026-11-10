using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows;

namespace Scanner.WPF.Services
{
    internal static class DashboardPluginLoader
    {
        internal static FrameworkElement Load(string baseDirectory, Version hostVersion)
        {
            string path = Path.GetFullPath(Path.Combine(baseDirectory, "Plugins", "Scanner.DashboardPlugin.dll"));
            if (!File.Exists(path)) return null;
            try
            {
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
                var type = assembly.GetType("Scanner.DashboardPlugin.PluginEntry", throwOnError: true);
                var create = type.GetMethod("CreateView", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(Version) }, null);
                if (create == null || !typeof(FrameworkElement).IsAssignableFrom(create.ReturnType))
                    throw new InvalidOperationException("Unsupported dashboard plugin entry point.");
                return (FrameworkElement)create.Invoke(null, new object[] { hostVersion });
            }
            catch (Exception ex)
            {
                // Optional UI must never prevent scanning when a DLL is broken/incompatible.
                Trace.TraceWarning("Dashboard plugin failed to load: {0}", ex);
                return null;
            }
        }
    }
}
