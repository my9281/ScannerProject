using System;
namespace Scanner.Controllers
{
    public interface IController : IDisposable { }
    public interface IControllerFactory<in TView, out TController> where TController : IController
    {
        TController Create(TView view);
    }
    public abstract class ControllerBase : IController
    {
        public virtual void Dispose() { }
    }
}
