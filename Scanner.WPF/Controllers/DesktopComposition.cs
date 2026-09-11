using Scanner.Helpers;
using Scanner.Helpers.Services;
using Microsoft.Extensions.DependencyInjection;
using Scanner.Controllers;
using Scanner.DI;
using Scanner.WPF.Helpers;
using Scanner.WPF.Services;
using Scanner.WPF.ViewModels;
namespace Scanner.WPF.Controllers
{
    internal static class DesktopComposition
    {
        internal static ServiceProvider Build()
        {
            var services = new ServiceCollection();
            services.AddScannerSharedServices();
            services.AddTransient(typeof(IControllerFactory<,>), typeof(WpfControllerFactory<,>));
            services.AddSingleton<IDesktopWindows, DesktopWindows>();
            services.AddTransient<MainWindow>();
            services.AddTransient<LoginWindow>();
            services.AddTransient<ChecklistWindow>();
            services.AddTransient<OutboundInspectionWindow>();
            services.AddTransient<LocationFeeComparisonWindow>();
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<AuthService>();
            services.AddTransient<NetworkHelper>();
            services.AddTransient<PrintingHelper>();
            services.AddTransient<ScanService>();
            services.AddTransient<ScanUploadService>();
            services.AddTransient<MeterModelService>();
            services.AddTransient<SpeechService>();
            services.AddTransient<WorkOrderSearchService>();
            services.AddTransient<UrgentWorkOrderImportHelper>();
            services.AddTransient<DialogHelper>();
            return services.BuildServiceProvider();
        }
    }
}
