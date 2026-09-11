using Scanner.Controllers;
namespace Scan.MaUI.Controllers;
public sealed class MauiControllerFactory<TView, TController> : IControllerFactory<TView, TController> where TController : class, IController
{
    private readonly IServiceProvider _services;
    public MauiControllerFactory(IServiceProvider services) => _services = services;
    public TController Create(TView view)
    {
        if (!MainThread.IsMainThread) throw new InvalidOperationException("Controllers must be created on the UI thread.");
        return ActivatorUtilities.CreateInstance<TController>(_services, view!);
    }
}
