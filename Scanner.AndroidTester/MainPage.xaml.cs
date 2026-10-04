namespace Scanner.AndroidTester;
public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        Title = "壹仓·PDA";
        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = Color.FromArgb("#F4F7F5");
        var layout = new VerticalStackLayout { Padding = new Thickness(24, 32, 24, 24), Spacing = 18, MaximumWidthRequest = 480, HorizontalOptions = LayoutOptions.Fill };
        layout.Add(new Label { Text = "壹 仓   /   移 动 作 业", FontSize = 12, CharacterSpacing = 3, TextColor = Color.FromArgb("#607C96"), HorizontalTextAlignment = TextAlignment.Center });
        layout.Add(new Image { Source = "porcelain_dragon.png", HeightRequest = 100, Aspect = Aspect.AspectFit });
          var scan = new Button { Text = "良品区扫描", HeightRequest = 72, FontSize = 22 };
        scan.Clicked += async (_, _) => {
            var s = Handler!.MauiContext!.Services;
            await Navigation.PushAsync(new NativeScanPage(NativeScanMode.Pallet, s.GetRequiredService<Services.ScanSession>(), s.GetRequiredService<Services.PalletScanSession>(), s.GetRequiredService<Services.PalletScanApiService>()));
        };
        layout.Add(scan);
        var print = new Button { Text = "打印标签", HeightRequest = 72, FontSize = 22 };
        print.Clicked += async (_, _) => await Navigation.PushAsync(new PrintLabelPage(Handler!.MauiContext!.Services.GetRequiredService<Services.LatestScan>()));
        layout.Add(print);
        layout.Add(new Label { Text = "扫描 · 归仓 · 标签", FontSize = 12, CharacterSpacing = 3, TextColor = Color.FromArgb("#607C96"), HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 14) });
         Content = new ScrollView { Content = layout };
    }
}
