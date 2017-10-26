using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using ReactiveDomain.Logging;

namespace ReactiveDomain.Tests.Logging
{
    // ReSharper disable once InconsistentNaming
    public abstract class with_message_logging_enabled :
        with_event_store
    {
        protected EventStoreMessageLogger Logging;
        protected string StreamName = $"Test-{Guid.NewGuid():N}-";

        protected override void Given()
        {
            base.Given();

            // instantiate Logger class that inherits from QueuedSubscriber
            Logging = new EventStoreMessageLogger(Bus,
                EventStore.Connection,
                StreamName,
                true);

            Thread.Sleep(2000); // needs a bit of time to set up the ES
        }

        #region IDisposable

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                Logging?.Dispose();
                //TODO: figure out how to delete test logging streams
            }
            _disposed = true;
            base.Dispose(disposing);
        }

        #endregion
    }
}
