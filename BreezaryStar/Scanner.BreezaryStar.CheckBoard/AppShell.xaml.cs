namespace Scanner.BreezaryStar.CheckBoard
{
    public partial class AppShell : Shell
    {
        public AppShell(Services.AccountClient accounts)
        {
            InitializeComponent();
            HomeContent.Content = new MainPage(accounts);
        }
    }
}
