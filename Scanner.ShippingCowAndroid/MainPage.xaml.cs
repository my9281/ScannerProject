using Scanner.ShippingCowAndroid.Services;
namespace Scanner.ShippingCowAndroid;
public partial class MainPage : ContentPage
{
    private ContentView receiptHistory = new() { HeightRequest = 360 };
    protected override void OnAppearing()
    {
        base.OnAppearing();
        ReloadReceiptHistory();
    }
    private void ReloadReceiptHistory()
    {
        // OnAppearing can precede handler attachment. Loaded retries after the native view is ready.
        if (Handler?.MauiContext?.Services is not { } services) return;
        try { receiptHistory.Content = new CarrierReceiptHistoryView(services.GetRequiredService<CarrierReceivingQueue>()); }
        catch (Exception)
        {
            receiptHistory.Content = new Label { Text = UiText.T("收货缓存无法读取，请保留文件并联系管理员。") };
        }
    }
    public MainPage()
    {
        InitializeComponent();
        Loaded += (_, _) => ReloadReceiptHistory();
        BuildMenu();
    }
    private void BuildMenu()
    {
        Title = UiText.AppName;
        NavigationPage.SetHasNavigationBar(this, false);
        var layout = new VerticalStackLayout { Padding = new Thickness(24, 32, 24, 24), Spacing = 18, MaximumWidthRequest = 480, HorizontalOptions = LayoutOptions.Fill };
        var languages = new Grid { ColumnSpacing = 6, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star), new(GridLength.Star) } };
        int languageColumn = 0;
        foreach (var (code, name) in new[] { ("zh", "中文"), ("en", "English"), ("es", "Español") })
        {
            var language = new Button { Text = name, FontSize = 13, Padding = new Thickness(4, 8), MinimumHeightRequest = 48,
                BackgroundColor = UiText.Language == code ? Color.FromArgb("#FEB81B") : Colors.White,
                TextColor = Color.FromArgb("#1A202C") };
            language.Clicked += (_, _) =>
            {
                UiText.Language = code;
                Preferences.Set(UiText.PreferenceKey, code);
                BuildMenu();
            };
            languages.Add(language, languageColumn++, 0);
        }
        layout.Add(languages);
        var logo = new Image { Source = "brand_logo.png", HeightRequest = 112, Aspect = Aspect.AspectFit };
        SemanticProperties.SetDescription(logo, UiText.AppName);
        layout.Add(logo);
        layout.Add(MenuButton("良品区扫描", "menu_scan.png", async () =>
        {
            var s = Handler!.MauiContext!.Services;
            await Navigation.PushAsync(new NativeScanPage(NativeScanMode.Pallet, s.GetRequiredService<Services.ScanSession>(), s.GetRequiredService<Services.PalletScanSession>(), s.GetRequiredService<Services.PalletScanApiService>()));
        }));
        layout.Add(MenuButton("托盘查询", "menu_lookup.png", async () =>
        {
            var services = Handler!.MauiContext!.Services;
            await Navigation.PushAsync(new PalletLookupPage(services.GetRequiredService<PalletScanApiService>(), services.GetRequiredService<AppSettings>()));
        }));
        layout.Add(MenuButton("包裹收货", "menu_receiving.png", async () =>
        {
            try
            {
                var services = Handler!.MauiContext!.Services;
                await Navigation.PushAsync(new CarrierReceivingPage(services.GetRequiredService<CarrierReceivingQueue>(), services.GetRequiredService<CarrierReceivingApi>(), services.GetRequiredService<AppSettings>()));
            }
            catch (Exception ex) { await DisplayAlertAsync(UiText.T("无法打开包裹收货"), UiText.Error(ex), UiText.T("确定")); }
        }));
        receiptHistory = new ContentView { HeightRequest = 360 };
        if (Handler?.MauiContext is not null) ReloadReceiptHistory();
        layout.Add(receiptHistory);
        Content = new ScrollView { Content = layout };
    }

    private static Grid MenuButton(string textKey, string iconSource, Func<Task> openPage)
    {
        string text = UiText.T(textKey);
        var button = new Button { MinimumHeightRequest = 68 };
        SemanticProperties.SetDescription(button, text);
        button.Clicked += async (_, _) => await openPage();

        var content = new Grid
        {
            InputTransparent = true,
            Padding = new Thickness(20, 10),
            ColumnSpacing = 12,
            ColumnDefinitions = { new(new GridLength(28)), new(GridLength.Star) }
        };
        var icon = new Image { Source = iconSource, HeightRequest = 28, VerticalOptions = LayoutOptions.Center };
        var label = new Label { Text = text, FontSize = 18, TextColor = Colors.White, VerticalOptions = LayoutOptions.Center };
        AutomationProperties.SetIsInAccessibleTree(icon, false);
        AutomationProperties.SetIsInAccessibleTree(label, false);
        content.Add(icon, 0, 0);
        content.Add(label, 1, 0);
        return new Grid { Children = { button, content } };
    }
}
