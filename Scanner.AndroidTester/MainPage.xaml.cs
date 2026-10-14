using Scanner.AndroidTester.Services;
namespace Scanner.AndroidTester;
public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        BuildMenu();
    }
    private void BuildMenu()
    {
        Title = UiText.T("壹仓·PDA");
        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = Color.FromArgb("#080A0C");
        var layout = new VerticalStackLayout { Padding = new Thickness(24, 32, 24, 24), Spacing = 18, MaximumWidthRequest = 480, HorizontalOptions = LayoutOptions.Fill };
        var languages = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.End };
        foreach (var (code, name) in new[] { ("en", "English"), ("es", "Español") })
        {
            var language = new Button { Text = name, FontSize = 14, Padding = new Thickness(12, 6),
                BackgroundColor = Color.FromArgb(UiText.Language == code ? "#365A70" : "#181C20"),
                TextColor = Colors.White };
            language.Clicked += (_, _) =>
            {
                UiText.Language = code;
                Preferences.Set(UiText.PreferenceKey, code);
                BuildMenu();
            };
            languages.Add(language);
        }
        layout.Add(languages);
        layout.Add(new Image { Source = "brand_logo.png", HeightRequest = 100, Aspect = Aspect.AspectFit });
          var scan = new Button { Text = UiText.T("良品区扫描"), HeightRequest = 72, FontSize = 22 };
        scan.Clicked += async (_, _) => {
            var s = Handler!.MauiContext!.Services;
            await Navigation.PushAsync(new NativeScanPage(NativeScanMode.Pallet, s.GetRequiredService<Services.ScanSession>(), s.GetRequiredService<Services.PalletScanSession>(), s.GetRequiredService<Services.PalletScanApiService>()));
        };
        layout.Add(scan);
        var print = new Button { Text = UiText.T("打印标签"), HeightRequest = 72, FontSize = 22 };
        print.Clicked += async (_, _) => await Navigation.PushAsync(new PrintLabelPage(Handler!.MauiContext!.Services.GetRequiredService<Services.LatestScan>()));
        layout.Add(print);
        var pairs = new Button { Text = UiText.T("SKU / SN 扫描"), HeightRequest = 72, FontSize = 22 };
        pairs.Clicked += async (_, _) => {
            var services = Handler!.MauiContext!.Services;
            await Navigation.PushAsync(new SkuSnScanPage(services.GetRequiredService<Services.SkuSnSession>(), services.GetRequiredService<Services.AppSettings>()));
        };
        layout.Add(pairs);
        layout.Add(new Label { Text = UiText.T("基础资料"), FontSize = 18, TextColor = Color.FromArgb("#92979D"), Margin = new Thickness(0, 12, 0, 0) });
        var locations = new Button { Text = UiText.T("库位扫描"), HeightRequest = 72, FontSize = 22 };
        locations.Clicked += async (_, _) => {
            var services = Handler!.MauiContext!.Services;
            await Navigation.PushAsync(new LocationScanPage(services.GetRequiredService<Services.LocationScanSession>(), services.GetRequiredService<Services.LocationScanApiService>()));
        };
        layout.Add(locations);
         Content = new ScrollView { Content = layout };
    }
}
