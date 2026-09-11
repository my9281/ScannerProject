namespace Scan.MaUI.Views;
public interface IMainPageView
{
    object BindingContext { get; set; }
    Entry ScanEntry { get; }
}
