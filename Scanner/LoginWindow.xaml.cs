using Scanner.Helpers;
using Scanner.Models;
using Scanner.Services;
using System;
using System.Windows;
using System.Windows.Input;

using Scanner.Controllers;
using Scanner.PlatformControllers;
namespace Scanner
{
    public partial class LoginWindow : Window, ILoginWindowView
    {
        private readonly LoginWindowController _controller;
        public LoginWindow(IControllerFactory<ILoginWindowView, LoginWindowController> factory)
        {
            InitializeComponent();
            _controller = factory.Create(this);
            Closed += (s, e) => _controller.Dispose();
        }
        Window ILoginWindowView.OwnerWindow => this;
        System.Windows.Controls.TextBox ILoginWindowView.UsernameTextBox => UsernameTextBox;
        System.Windows.Controls.PasswordBox ILoginWindowView.PasswordInput => PasswordInput;
        System.Windows.Controls.CheckBox ILoginWindowView.RememberPasswordCheckBox => RememberPasswordCheckBox;
        System.Windows.Controls.Button ILoginWindowView.LoginButton => LoginButton;
        System.Windows.Controls.Button ILoginWindowView.LocalModeButton => LocalModeButton;
        System.Windows.Controls.TextBlock ILoginWindowView.StatusTextBlock => StatusTextBlock;
        public AuthSession Session => _controller.Session;
        private void Window_Loaded(object sender, RoutedEventArgs e) => _controller.Window_Loaded(sender, e);
        private void LanguageButton_Click(object sender, RoutedEventArgs e) => _controller.LanguageButton_Click(sender, e);
        private void Input_KeyDown(object sender, KeyEventArgs e) => _controller.Input_KeyDown(sender, e);
        private void LoginButton_Click(object sender, RoutedEventArgs e) => _controller.LoginButton_Click(sender, e);
        private void LocalModeButton_Click(object sender, RoutedEventArgs e) => _controller.LocalModeButton_Click(sender, e);
    }
}
