using Scanner.Models;
using Microsoft.Extensions.DependencyInjection;
using Scanner.Services;
using System;
using System.Windows;

namespace Scanner
{
    public partial class App : Application
    {
        private readonly ServiceProvider _services = PlatformControllers.DesktopComposition.Build();

        protected override void OnExit(ExitEventArgs e)
        {
            _services.Dispose();
            base.OnExit(e);
        }

        public static AuthSession CurrentSession { get; private set; }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                AuthSession session = LocalAuthStore.LoadSession();
                if (session != null && session.IsValid())
                {
                    CurrentSession = session;
                    OpenMainWindow();
                    return;
                }
                LocalAuthStore.ClearSession();
                OpenLoginWindow();
            }
            catch (Exception ex)
            {
                MessageBox.Show("程序启动失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void OpenLoginWindow()
        {
            LoginWindow loginWindow = ((App)Current)._services.GetRequiredService<LoginWindow>();
            bool? result = loginWindow.ShowDialog();
            if (result == true && loginWindow.Session != null)
            {
                CurrentSession = loginWindow.Session;
                OpenMainWindow();
            }
            else
            {
                Shutdown();
            }
        }

        private void OpenMainWindow()
        {
            MainWindow mainWindow = _services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }

        public static void Logout()
        {
            LocalAuthStore.ClearSession();
            CurrentSession = null;
            Window currentMainWindow = Current.MainWindow;
            LoginWindow loginWindow = ((App)Current)._services.GetRequiredService<LoginWindow>();
            bool? result = loginWindow.ShowDialog();
            if (result == true && loginWindow.Session != null)
            {
                CurrentSession = loginWindow.Session;
                MainWindow newMainWindow = ((App)Current)._services.GetRequiredService<MainWindow>();
                Current.MainWindow = newMainWindow;
                newMainWindow.Show();
                if (currentMainWindow != null)
                {
                    currentMainWindow.Close();
                }
            }
            else
            {
                Current.Shutdown();
            }
        }
    }
}
