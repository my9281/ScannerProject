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
    }
    protected override async void OnAppearing(){base.OnAppearing();if(!_initialized){_initialized=true;await _viewModel.InitializeAsync();}ScanEntry.Focus();}
}
