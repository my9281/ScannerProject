namespace Scan.MaUI.Windows;
public sealed class MauiWindow : IWindow
{
    private readonly IServiceProvider _services;
    public MauiWindow(IServiceProvider services) => _services = services;
    private static Microsoft.Maui.Controls.Window Current => Application.Current?.Windows.FirstOrDefault()
        ?? throw new InvalidOperationException("No active application window.");
    private static Page Page => Current.Page ?? throw new InvalidOperationException("No active page.");
    public void ShowMain() => Current.Page = new NavigationPage(_services.GetRequiredService<MainPage>());
    public void ShowLogin() => Current.Page = new NavigationPage(_services.GetRequiredService<LoginPage>());
    public Task OpenOperationsAsync() => Page.Navigation.PushAsync(_services.GetRequiredService<OperationsPage>());
    public Task ShowAlertAsync(string title, string message, string cancel) => Page.Navigation.NavigationStack.Last().DisplayAlertAsync(title, message, cancel);
    public Task<string?> SelectActionAsync(string title, string cancel, string[] options) => Page.Navigation.NavigationStack.Last().DisplayActionSheetAsync(title, cancel, null, options);
    public void Dispatch(Action action) => MainThread.BeginInvokeOnMainThread(action);
}
