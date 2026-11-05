using Microsoft.Extensions.DependencyInjection;
using Scanner.DI;
using Scanner.Helpers.Services;
using Scanner.MaUI.Services;
using Scanner.MaUI.ViewModels;

namespace Scanner.MaUI.Controllers;

internal static class MauiComposition
{
    internal static IServiceCollection AddScannerMaui(this IServiceCollection services)
    {
        services.AddSingleton(new HttpClient { BaseAddress = new Uri("https://wms.ymforever.com/"), Timeout = TimeSpan.FromSeconds(60) });
        services.AddScannerSharedServices();
        services.AddTransient(typeof(Scanner.Controllers.IControllerFactory<,>), typeof(MauiControllerFactory<,>));
        services.AddSingleton<Windows.IWindow, Windows.MauiWindow>();
        services.AddSingleton<AuthService>();
        services.AddSingleton<SessionStore>();
        services.AddSingleton<OidService>();
        services.AddSingleton<ScanLogService>();
        services.AddSingleton<ScanService>();
        services.AddSingleton<MeterModelService>();
        services.AddSingleton<SpeechService>();
        services.AddSingleton<ScanUploadService>();
        services.AddSingleton<ShelvedPalletApiService>();
        services.AddSingleton<WorkOrderSearchService>();
        services.AddSingleton<WorkOrderRemarkApiService>();
        services.AddSingleton<CsvImportService>();
        services.AddSingleton(LocalizationService.Current);
        services.AddSingleton<ILabelPrinter, LabelPrinter>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<MainPage>();
        services.AddTransient<OperationsPage>();
        services.AddTransient<LoginPage>();
        return services;
    }
}
