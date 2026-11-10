using System;
namespace Scanner.WPF.ViewModels
{
    public abstract class ViewModelBase : Scanner.ViewModels.ViewModelBase
    {
        protected override void DispatchNotification(Action notification)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess()) notification();
            else dispatcher.BeginInvoke(notification);
        }
    }
}
