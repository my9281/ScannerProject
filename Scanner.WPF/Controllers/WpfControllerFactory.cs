using Microsoft.Extensions.DependencyInjection;
using Scanner.Controllers;
using System;
namespace Scanner.WPF.Controllers
{
    public sealed class WpfControllerFactory<TView, TController> : IControllerFactory<TView, TController> where TController : class, IController
    {
        private readonly IServiceProvider _services;
        public WpfControllerFactory(IServiceProvider services) { _services = services; }
        public TController Create(TView view)
        {
            System.Windows.Application.Current?.Dispatcher.VerifyAccess();
            return ActivatorUtilities.CreateInstance<TController>(_services, view);
        }
    }
}
