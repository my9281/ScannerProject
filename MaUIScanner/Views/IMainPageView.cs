namespace MaUIScanner.Views;
public interface IMainPageView
{
    object BindingContext { get; set; }
    Entry ScanEntry { get; }
}
