using System;
namespace ReactiveDomain.Util
{
    public class Disposer:IDisposable
    {
        private Func<Unit> _disposeFunc;

        public Disposer(Func<Unit> disposeFunc)
        {
            _disposeFunc = disposeFunc ?? throw new ArgumentNullException(nameof(disposeFunc));
        }

        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            try{
                _disposeFunc();
                _disposeFunc = null;
            }
            catch {
               //ignore
            }
            _disposed = true;
        }
    }
}