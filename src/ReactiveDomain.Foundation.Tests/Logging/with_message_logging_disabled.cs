using System;
using System.Threading;
using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Foundation.Tests.Logging
{
    // ReSharper disable once InconsistentNaming
    public abstract class with_message_logging_disabled : IDisposable
    {
        protected readonly IStreamStoreConnection Connection;
        protected IDispatcher Bus;
        protected with_message_logging_disabled(IStreamStoreConnection connection):this()
        {
            Connection = connection;
            Bus = new Dispatcher(nameof(with_message_logging_enabled));
        }
        //protected member Logging class that inherits from QueuedSubscriber
        protected EventStoreMessageLogger Logging;
        protected string StreamName = $"LogTest-{Guid.NewGuid():N}";
        protected StreamStoreRepository Repo;
        protected IStreamNameBuilder StreamNameBuilder;
        protected IEventSerializer EventSerializer;
        protected with_message_logging_disabled()
        {
            StreamNameBuilder = new PrefixedCamelCaseStreamNameBuilder("UnitTest");
            EventSerializer = new JsonMessageSerializer();
            Repo = new StreamStoreRepository(StreamNameBuilder, Connection, new JsonMessageSerializer());
            // ctor defaults to disabled
            Logging = new EventStoreMessageLogger(Bus,
                Connection,
                StreamName);

            Thread.Sleep(1000);
        }
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposing) return;
           
            Bus?.Dispose();
        }
    }
}
