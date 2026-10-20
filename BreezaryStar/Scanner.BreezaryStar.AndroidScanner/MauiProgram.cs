using Scanner.BreezaryStar.AndroidScanner.Services;

namespace Scanner.BreezaryStar.AndroidScanner;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder().UseMauiApp<App>();
        builder.Services.AddSingleton(new InboundCache(Path.Combine(FileSystem.AppDataDirectory, "inbound-cache")));
        builder.Services.AddSingleton(new InboundUploadClient(new HttpClient(new HttpClientHandler
        { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(60) }));
        return builder.Build();
    }
}
