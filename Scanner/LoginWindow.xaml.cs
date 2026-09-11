using Scanner.Helpers;
using Scanner.Models;
using Scanner.Services;
using System;
using System.Windows;
using System.Windows.Input;

namespace Scanner
{
    public partial class LoginWindow : Window
    {
        private readonly AuthService _authService;
        private bool _isLoggingIn;
        public AuthSession Session { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
            _authService = new AuthService();
            _isLoggingIn = false;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadRememberedLogin();
        }

        private void LanguageButton_Click(object sender, RoutedEventArgs e)
        {
            UiText.ChangeLanguage((string)((System.Windows.Controls.Button)sender).Tag);
            StatusTextBlock.Text = string.Empty;
        }

        private void LoadRememberedLogin()
        {
            RememberedLogin remembered = LocalAuthStore.LoadRememberedLogin();
            if (remembered == null)
            {
                UsernameTextBox.Focus();
                return;
            }
            UsernameTextBox.Text = remembered.Username ?? string.Empty;
            PasswordInput.Password = remembered.Password ?? string.Empty;
            RememberPasswordCheckBox.IsChecked = remembered.RememberPassword;
            if (!string.IsNullOrWhiteSpace(PasswordInput.Password))
            {
                LoginButton.Focus();
            }
            else
            {
                PasswordInput.Focus();
            }
        }

        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }
            e.Handled = true;
            StartLogin();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            StartLogin();
        }

        private void LocalModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoggingIn)
            {
                return;
            }
            Session = new AuthSession
            {
                Operator = "本地扫描",
                Role = "本地模式",
                IsLocalMode = true
            };
            DialogResult = true;
        }

        private async void StartLogin()
        {
            if (_isLoggingIn)
            {
                return;
            }
            string username = UsernameTextBox.Text.Trim();
            string password = PasswordInput.Password;
            if (string.IsNullOrWhiteSpace(username))
            {
                ShowError(UiText.Get("UsernameRequired"));
                UsernameTextBox.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError(UiText.Get("PasswordRequired"));
                PasswordInput.Focus();
                return;
            }
            _isLoggingIn = true;
            SetControlsEnabled(false);
            StatusTextBlock.Foreground = System.Windows.Media.Brushes.DarkBlue;
            StatusTextBlock.Text = UiText.Get("SigningIn");
            try
            {
                AuthSession session = await _authService.LoginAsync(username, password);
                LocalAuthStore.SaveSession(session);
                bool rememberPassword = RememberPasswordCheckBox.IsChecked == true;
                LocalAuthStore.SaveRememberedLogin(username, password, rememberPassword);
                Session = session;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
                PasswordInput.SelectAll();
                PasswordInput.Focus();
            }
            finally
            {
                _isLoggingIn = false;
                SetControlsEnabled(true);
            }
        }

        private void SetControlsEnabled(bool isEnabled)
        {
            UsernameTextBox.IsEnabled = isEnabled;
            PasswordInput.IsEnabled = isEnabled;
            RememberPasswordCheckBox.IsEnabled = isEnabled;
            LoginButton.IsEnabled = isEnabled;
            LocalModeButton.IsEnabled = isEnabled;
        }

        private void ShowError(string message)
        {
            StatusTextBlock.Foreground = System.Windows.Media.Brushes.DarkRed;
            StatusTextBlock.Text = message;
        }
    }
}
