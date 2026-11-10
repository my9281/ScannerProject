using Scanner.BreezaryStar.AndroidScanner.Services;

namespace Scanner.BreezaryStar.AndroidScanner;

public partial class App : Application
{
    private readonly InboundCache cache;
    private readonly InboundUploadClient upload;
    public App(InboundCache cache, InboundUploadClient upload)
    {
        this.cache = cache; this.upload = upload;
        InitializeComponent();
    }
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new NavigationPage(new MainPage(cache, upload))) { Title = "风扇PDA" };
}
