using Microsoft.Extensions.DependencyInjection;

namespace Scanner.CheckListBoard
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
            return new Window(new LoginPage(accounts));
        }
    }
}
