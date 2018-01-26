using System;
using System.Threading;
using ReactiveDomain.Foundation.EventStore;

namespace ReactiveDomain.Tests.Logging
{
    // ReSharper disable once InconsistentNaming
    public abstract class with_message_logging_disabled :
        with_event_store,
        IDisposable
    {
        // set the skip reason to "" to run the tests
        protected const string SkipReason = "Stream Cleanup Required";
        
        //protected member Logging class that inherits from QueuedSubscriber
        protected EventStoreMessageLogger Logging;
        protected string StreamName = $"LogTest-{Guid.NewGuid():N}";

        protected override void Given()
        {
            base.Given();

            // ctor defaults to disabled
            Logging = new EventStoreMessageLogger(Bus,
                EventStore.Connection,
                StreamName);

            Thread.Sleep(1000);
        }

        #region IDisposable

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                Logging?.Dispose();
            }
            _disposed = true;
            base.Dispose(disposing);
        }

        #endregion
    }
}
