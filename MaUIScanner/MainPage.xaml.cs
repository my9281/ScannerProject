using MaUIScanner.ViewModels;

namespace MaUIScanner;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.FocusRequested += (_, _) => MainThread.BeginInvokeOnMainThread(() => ScanEntry.Focus());
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        ScanEntry.Focus();
    }
}
