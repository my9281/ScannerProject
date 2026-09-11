using System;
namespace Scanner.ViewModels
{
    public abstract class ViewModelBase : Scanner.Presentation.ViewModelBase
    {
        protected override void DispatchNotification(Action notification)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess()) notification();
            else dispatcher.BeginInvoke(notification);
        }
    }
}
