namespace Scanner.CheckListBoard
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
