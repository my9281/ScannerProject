using Microsoft.Extensions.DependencyInjection;

namespace Scanner.BreezaryStar.CheckBoard
{
    public partial class App : Application
    {
        private readonly Services.AccountClient accounts;
        public App(Services.AccountClient accounts)
        {
            this.accounts = accounts;
            InitializeComponent();
            UserAppTheme = AppTheme.Dark;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new LoginPage(accounts)) { Title = "风扇平板" };
        }
    }
}
