using System;
using System.Threading;
using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Foundation.Tests.Logging
{
    // ReSharper disable once InconsistentNaming
    public abstract class with_message_logging_enabled :IDisposable
    {
        protected readonly IStreamStoreConnection Connection;
        protected IDispatcher Bus;
        protected EventStoreMessageLogger Logging;
        protected string StreamName = $"LogTest-{Guid.NewGuid():N}";
        protected StreamStoreRepository Repo;
        protected PrefixedCamelCaseStreamNameBuilder StreamNameBuilder;
        protected IEventSerializer EventSerializer;

        protected with_message_logging_enabled(IStreamStoreConnection connection)
        {
            Connection = connection;
            Bus = new Dispatcher(nameof(with_message_logging_enabled));
            StreamNameBuilder = new PrefixedCamelCaseStreamNameBuilder("UnitTest");
            EventSerializer = new JsonMessageSerializer(); 
            Repo = new StreamStoreRepository(StreamNameBuilder, Connection, new JsonMessageSerializer());
            // instantiate Logger class that inherits from QueuedSubscriber
            Logging = new EventStoreMessageLogger(Bus,
                Connection,
                StreamName,
                true);
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
