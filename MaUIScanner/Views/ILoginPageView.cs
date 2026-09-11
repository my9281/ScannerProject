namespace MaUIScanner.Views;
public interface ILoginPageView
{
    Entry UsernameEntry { get; }
    Entry PasswordEntry { get; }
    CheckBox RememberCheckBox { get; }
    Button LoginButton { get; }
    Button LocalButton { get; }
    ActivityIndicator BusyIndicator { get; }
    Label StatusLabel { get; }
}
