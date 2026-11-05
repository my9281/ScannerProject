using System;
using System.Windows;

namespace Scanner.DashboardPlugin
{
    // Runtime entry point v1: only framework types cross the host/plugin boundary.
    public static class PluginEntry
    {
        public static FrameworkElement CreateView(Version hostVersion)
            => new DashboardWidgets(hostVersion ?? throw new ArgumentNullException(nameof(hostVersion)));
    }
}
