using System;
using System.Threading;
using ReactiveDomain.Foundation.EventStore;

namespace ReactiveDomain.Foundation.Tests.Logging
{
    // ReSharper disable once InconsistentNaming
    public abstract class with_message_logging_enabled :
        with_event_store
    {
        // set the skip reason to "" to run the tests
        protected const string SkipReason = "Stream Cleanup Required";
        protected EventStoreMessageLogger Logging;
        protected string StreamName = $"LogTest-{Guid.NewGuid():N}";

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
