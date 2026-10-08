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

            var settings = Services.AppSettings.Load();
            builder.Services.AddSingleton(settings);
            builder.Services.AddSingleton(new Services.SkuSnSession(Path.Combine(FileSystem.AppDataDirectory, "sku-sn-batch.json")));
            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddSingleton<Services.ScanSession>();
            builder.Services.AddSingleton<Services.LatestScan>();
            builder.Services.AddSingleton(new Services.PalletScanSession { ApiKey = settings.UploadApiKey });
            builder.Services.AddSingleton(new Services.LocationScanSession { ApiKey = settings.UploadApiKey });
            builder.Services.AddSingleton(new Services.LocationScanApiService(new HttpClient { BaseAddress = settings.UploadBaseUrl, Timeout = TimeSpan.FromSeconds(60) }));
            builder.Services.AddSingleton(new Services.PalletScanApiService(new HttpClient
            {
                BaseAddress = settings.UploadBaseUrl,
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
