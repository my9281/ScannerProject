using Microsoft.Extensions.Logging;
using Scanner.DI;
using Scanner.MaUI.Services;
using Scanner.MaUI.ViewModels;

namespace Scanner.MaUI
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder.UseMauiApp<App>().ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("Bickham Script Pro Semibold.ttf", "BickhamScriptProSemibold");
                fonts.AddFont("GenJyuuGothic.ttf", "GenJyuuGothic");
                fonts.AddFont("IMing.ttf", "IMingPrint");
                fonts.AddFont("ZiHunShouJin.ttf", "ZiHunShouJinPrint");
            });
            builder.Services.AddSingleton(new HttpClient { BaseAddress = new Uri("https://wms.ymforever.com/"), Timeout = TimeSpan.FromSeconds(30) });
            builder.Services.AddScannerSharedServices();
            builder.Services.AddTransient(typeof(Scanner.Controllers.IControllerFactory<,>), typeof(Controllers.MauiControllerFactory<,>));
            builder.Services.AddSingleton<Windows.IWindow, Windows.MauiWindow>();
            builder.Services.AddSingleton<AuthService>();
            builder.Services.AddSingleton<SessionStore>();
            builder.Services.AddSingleton<OidService>();
            builder.Services.AddSingleton<ScanLogService>();
            builder.Services.AddSingleton<ScanService>();
            builder.Services.AddSingleton<MeterModelService>();
            builder.Services.AddSingleton<SpeechService>();
            builder.Services.AddSingleton<ScanUploadService>();
            builder.Services.AddSingleton<WorkOrderSearchService>();
            builder.Services.AddSingleton<WorkOrderRemarkApiService>();
            builder.Services.AddSingleton<CsvImportService>();
            builder.Services.AddSingleton(LocalizationService.Current);
            builder.Services.AddSingleton<ILabelPrinter, LabelPrinter>();
            builder.Services.AddTransient<MainViewModel>();
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<OperationsPage>();
            builder.Services.AddTransient<LoginPage>();
#if DEBUG
    		builder.Logging.AddDebug();
#endif
            return builder.Build();
        }
    }
}
