using Microsoft.Extensions.Logging;

namespace Scanner.AndroidTester
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddSingleton<Services.ScanSession>();
            builder.Services.AddSingleton<Services.LatestScan>();
            builder.Services.AddSingleton<Services.PalletScanSession>();
            builder.Services.AddSingleton(new Services.PalletScanApiService(new HttpClient
            {
                BaseAddress = new Uri("https://wms.ymforever.com/"),
                Timeout = TimeSpan.FromSeconds(60)
            }));

#if DEBUG
    		builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
