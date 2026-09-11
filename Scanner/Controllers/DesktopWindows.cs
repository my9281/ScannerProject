using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
namespace Scanner.PlatformControllers
{
    public interface IDesktopWindows
    {
        void ShowChecklist(Window owner);
        void ShowOutbound(Window owner);
        void ShowLocationFee(Window owner);
    }
    public sealed class DesktopWindows : IDesktopWindows
    {
        private readonly IServiceProvider _services;
        public DesktopWindows(IServiceProvider services) { _services = services; }
        private void Show<T>(Window owner) where T : Window { var view = _services.GetRequiredService<T>(); view.Owner = owner; view.ShowDialog(); }
        public void ShowChecklist(Window owner) => Show<ChecklistWindow>(owner);
        public void ShowOutbound(Window owner) => Show<OutboundInspectionWindow>(owner);
        public void ShowLocationFee(Window owner) => Show<LocationFeeComparisonWindow>(owner);
    }
}
