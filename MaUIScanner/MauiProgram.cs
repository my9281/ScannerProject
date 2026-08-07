using Microsoft.Extensions.Logging;
using MaUIScanner.Services;
using MaUIScanner.ViewModels;

namespace MaUIScanner
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
            });
            builder.Services.AddSingleton(new HttpClient { BaseAddress = new Uri("https://wms.ymforever.com/"), Timeout = TimeSpan.FromSeconds(30) });
            builder.Services.AddSingleton<OidService>();
            builder.Services.AddSingleton<ScanLogService>();
            builder.Services.AddSingleton<ScanService>();
            builder.Services.AddSingleton<MeterModelService>();
            builder.Services.AddSingleton<SpeechService>();
            builder.Services.AddSingleton<ScanUploadService>();
            builder.Services.AddSingleton<ILabelPrinter, LabelPrinter>();
            builder.Services.AddTransient<MainViewModel>();
            builder.Services.AddTransient<MainPage>();
#if DEBUG
    		builder.Logging.AddDebug();
#endif
            return builder.Build();
        }
    }
}
