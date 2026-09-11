using System.Runtime.CompilerServices;
namespace MaUIScanner.ViewModels;
public abstract class ViewModelBase : Scanner.Presentation.ViewModelBase
{
    protected override void DispatchNotification(Action notification)
    {
        if (MainThread.IsMainThread) notification();
        else MainThread.BeginInvokeOnMainThread(notification);
    }
    protected void Notify([CallerMemberName] string? name = null) => RaisePropertyChanged(name);
}
// Compatibility for existing callers.
public abstract class ObservableObject : ViewModelBase { }

