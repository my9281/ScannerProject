using Scanner.Controllers;
using Scanner.Models;
using Scanner.WPF.Helpers;
using Scanner.WPF.Services;
using System;
using System.Windows;
using System.Windows.Input;
namespace Scanner.WPF.Controllers
{
    public sealed class LoginWindowController : ControllerBase
    {
        private readonly ILoginWindowView _view;

        private readonly AuthService _authService;
        private bool _isLoggingIn;
        public AuthSession Session { get; private set; }

        public LoginWindowController(ILoginWindowView view, AuthService authService)
        {
            _view = view;

            _authService = authService;
            _isLoggingIn = false;

        }

        public void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadRememberedLogin();
        }

        public void LanguageButton_Click(object sender, RoutedEventArgs e)
        {
            UiText.ChangeLanguage((string)((System.Windows.Controls.Button)sender).Tag);
            _view.StatusTextBlock.Text = string.Empty;
        }

        private void LoadRememberedLogin()
        {
            RememberedLogin remembered = LocalAuthStore.LoadRememberedLogin();
            if (remembered == null)
            {
                _view.UsernameTextBox.Focus();
                return;
            }
            _view.UsernameTextBox.Text = remembered.Username ?? string.Empty;
            _view.PasswordInput.Password = remembered.Password ?? string.Empty;
            _view.RememberPasswordCheckBox.IsChecked = remembered.RememberPassword;
            if (!string.IsNullOrWhiteSpace(_view.PasswordInput.Password))
            {
                _view.LoginButton.Focus();
            }
            else
            {
                _view.PasswordInput.Focus();
            }
        }

        public void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }
            e.Handled = true;
            StartLogin();
        }

        public void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            StartLogin();
        }

        public void LocalModeButton_Click(object sender, RoutedEventArgs e)
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
            _view.OwnerWindow.DialogResult = true;
        }

        private async void StartLogin()
        {
            if (_isLoggingIn)
            {
                return;
            }
            string username = _view.UsernameTextBox.Text.Trim();
            string password = _view.PasswordInput.Password;
            if (string.IsNullOrWhiteSpace(username))
            {
                ShowError(UiText.Get("UsernameRequired"));
                _view.UsernameTextBox.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError(UiText.Get("PasswordRequired"));
                _view.PasswordInput.Focus();
                return;
            }
            _isLoggingIn = true;
            SetControlsEnabled(false);
            _view.StatusTextBlock.Foreground = System.Windows.Media.Brushes.DarkBlue;
            _view.StatusTextBlock.Text = UiText.Get("SigningIn");
            try
            {
                AuthSession session = await _authService.LoginAsync(username, password);
                LocalAuthStore.SaveSession(session);
                bool rememberPassword = _view.RememberPasswordCheckBox.IsChecked == true;
                LocalAuthStore.SaveRememberedLogin(username, password, rememberPassword);
                Session = session;
                _view.OwnerWindow.DialogResult = true;
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
                _view.PasswordInput.SelectAll();
                _view.PasswordInput.Focus();
            }
            finally
            {
                _isLoggingIn = false;
                SetControlsEnabled(true);
            }
        }

        private void SetControlsEnabled(bool isEnabled)
        {
            _view.UsernameTextBox.IsEnabled = isEnabled;
            _view.PasswordInput.IsEnabled = isEnabled;
            _view.RememberPasswordCheckBox.IsEnabled = isEnabled;
            _view.LoginButton.IsEnabled = isEnabled;
            _view.LocalModeButton.IsEnabled = isEnabled;
        }

        private void ShowError(string message)
        {
            _view.StatusTextBlock.Foreground = System.Windows.Media.Brushes.DarkRed;
            _view.StatusTextBlock.Text = message;
        }

    }
}
