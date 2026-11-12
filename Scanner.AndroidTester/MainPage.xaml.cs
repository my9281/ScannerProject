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
        foreach (var (code, name) in new[] { ("zh", "中文"), ("en", "English"), ("es", "Español") })
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
          var scan = new Button { ImageSource = "menu_scan.png", ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Left, 12), Text = UiText.T("良品区扫描"), HeightRequest = 72, FontSize = 22 };
        scan.Clicked += async (_, _) => {
            var s = Handler!.MauiContext!.Services;
            await Navigation.PushAsync(new NativeScanPage(NativeScanMode.Pallet, s.GetRequiredService<Services.ScanSession>(), s.GetRequiredService<Services.PalletScanSession>(), s.GetRequiredService<Services.PalletScanApiService>()));
        };
        layout.Add(scan);
        var lookup = new Button { ImageSource = "menu_lookup.png", ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Left, 12), Text = UiText.T("托盘查询"), HeightRequest = 72, FontSize = 22 };
        lookup.Clicked += async (_, _) => {
            var services = Handler!.MauiContext!.Services;
            await Navigation.PushAsync(new PalletLookupPage(services.GetRequiredService<PalletScanApiService>(), services.GetRequiredService<AppSettings>()));
        };
        layout.Add(lookup);
        var records = new Button { ImageSource = "menu_pairs.png", Text = "扫描记录", HeightRequest = 72, FontSize = 22 };
        records.Clicked += async (_, _) => await Navigation.PushAsync(new RepairScanPage(Handler!.MauiContext!.Services.GetRequiredService<AppSettings>()));
        layout.Add(records);
         Content = new ScrollView { Content = layout };
    }
}
