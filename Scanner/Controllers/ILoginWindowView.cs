using System.Windows;
using System.Windows.Controls;
namespace Scanner.PlatformControllers
{
    public interface ILoginWindowView
    {
        Window OwnerWindow { get; }
        TextBox UsernameTextBox { get; }
        PasswordBox PasswordInput { get; }
        CheckBox RememberPasswordCheckBox { get; }
        Button LoginButton { get; }
        Button LocalModeButton { get; }
        TextBlock StatusTextBlock { get; }
    }
}
