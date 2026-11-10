using Microsoft.Extensions.Logging;
using Scanner.MaUI.Controllers;

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
                fonts.AddFont("GenJyuuGothic.ttf", "GenJyuuGothic");
#if MACCATALYST
                fonts.AddFont("IMing.ttf", "IMingPrint");
#endif
            });
            builder.Services.AddScannerMaui();
#if DEBUG
            builder.Logging.AddDebug();
#endif
            return builder.Build();
        }
    }
}
