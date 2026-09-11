using MaUIScanner.ViewModels;

namespace MaUIScanner;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel; private bool _initialized;
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent(); BindingContext=_viewModel=viewModel;
        _viewModel.FocusRequested+=(_,_)=>MainThread.BeginInvokeOnMainThread(()=>ScanEntry.Focus());
        _viewModel.LogoutRequested+=(_,_)=>MainThread.BeginInvokeOnMainThread(()=>((App)Application.Current!).ShowLoginPage());
        _viewModel.ModelSelectionRequested = SelectModelAsync;
    }

    private async Task<string?> SelectModelAsync(IReadOnlyList<string> models)
    {
        string? selected = await DisplayActionSheetAsync("选择电表型号", "取消", null, models.ToArray());
        return selected == "取消" ? null : selected;
    }
    protected override async void OnAppearing(){base.OnAppearing();if(!_initialized){_initialized=true;await _viewModel.InitializeAsync();}ScanEntry.Focus();}
    private void LanguageButton_Clicked(object? sender, EventArgs e)
    {
        Services.LocalizationService.Current.Change((string)((Button)sender!).CommandParameter);
        _viewModel.RefreshLocalizedText();
        ScanEntry.Focus();
    }

    private async void OperationsButton_Clicked(object? sender, EventArgs e)
    {
        var page = Handler?.MauiContext?.Services.GetRequiredService<OperationsPage>();
        if (page is not null) await Navigation.PushAsync(page);
    }
}
