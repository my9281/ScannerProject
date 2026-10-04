namespace Scanner.AndroidTester
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new NavigationPage(new MainPage())) { Title = "壹仓·PDA" };
        }
    }
}

