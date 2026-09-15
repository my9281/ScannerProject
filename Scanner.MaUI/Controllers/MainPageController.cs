using Scanner.Controllers;
using Scanner.MaUI.ViewModels;
using Scanner.MaUI.Views;
namespace Scanner.MaUI.Controllers;

public sealed class MainPageController : ControllerBase
{
    private readonly IMainPageView _view;
    private readonly Scanner.MaUI.Windows.IWindow _window;

    private readonly MainViewModel _viewModel; private bool _initialized;
    public MainPageController(IMainPageView view, Scanner.MaUI.Windows.IWindow window, MainViewModel viewModel) { _view = view; _window = window; _viewModel = viewModel; _view.BindingContext = viewModel; }

    private async Task<string?> SelectModelAsync(IReadOnlyList<string> models)
    {
        string? selected = await _window.SelectActionAsync("选择电表型号", "取消", models.ToArray());
        return selected == "取消" ? null : selected;
    }
    public async Task AppearingAsync()
    {
        Attach();
        if (!_initialized) { await _viewModel.InitializeAsync(); _initialized = true; }
        _view.ScanEntry.Focus();
    }
    public void LanguageButton_Clicked(object? sender, EventArgs e)
    {
        Services.LocalizationService.Current.Change((string)((Button)sender!).CommandParameter);
        _viewModel.RefreshLocalizedText();
        _view.ScanEntry.Focus();
    }

    public async void OperationsButton_Clicked(object? sender, EventArgs e)
    {
        try { await _window.OpenOperationsAsync(); }
        catch (Exception ex) { await _window.ShowAlertAsync("打开失败", ex.Message, "确定"); }
    }

    private bool _attached;
    private void Attach()
    {
        if (_attached) return;
        _attached = true;
        _viewModel.FocusRequested += FocusRequested;
        _viewModel.LogoutRequested += LogoutRequested;
        _viewModel.ModelSelectionRequested = SelectModelAsync;
    }
    private void FocusRequested(object? sender, EventArgs e) => _window.Dispatch(() => _view.ScanEntry.Focus());
    private void LogoutRequested(object? sender, EventArgs e) => _window.Dispatch(_window.ShowLogin);
    public override void Dispose()
    {
        if (!_attached) return;
        _attached = false;
        _viewModel.FocusRequested -= FocusRequested;
        _viewModel.LogoutRequested -= LogoutRequested;
        _viewModel.ModelSelectionRequested = null;
    }

}
