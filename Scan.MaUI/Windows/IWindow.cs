namespace Scan.MaUI.Windows;
// Framework-local window abstraction. Controllers never navigate via Application.Current.
public interface IWindow
{
    void ShowMain();
    void ShowLogin();
    Task OpenOperationsAsync();
    Task ShowAlertAsync(string title, string message, string cancel);
    Task<string?> SelectActionAsync(string title, string cancel, string[] options);
    void Dispatch(Action action);
}
