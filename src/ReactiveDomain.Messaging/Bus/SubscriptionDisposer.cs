using System;
using ReactiveDomain.Util;

namespace ReactiveDomain.Messaging.Bus
{
    public class SubscriptionDisposer:IDisposable
    {
        private readonly Func<Unit> _unsuscribe;

        public SubscriptionDisposer(Func<Unit> unsuscribe)
        {
            if (unsuscribe == null) throw new ArgumentNullException(nameof(unsuscribe));
            _unsuscribe = unsuscribe;
        }

        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            try
            {
                _unsuscribe();
            }
            catch 
            {
               //ignore
            }
            _disposed = true;
        }

      
    }
}