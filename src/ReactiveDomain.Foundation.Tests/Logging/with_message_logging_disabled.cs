using System;
using System.Threading;
using EventStore.ClientAPI;
using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging.Tests.Specifications;

namespace ReactiveDomain.Foundation.Tests.Logging
{
    // ReSharper disable once InconsistentNaming
    public abstract class with_message_logging_disabled : CommandBusSpecification
    {
        private readonly IEventStoreConnection _connection;
        
        protected with_message_logging_disabled(IEventStoreConnection connection)
        {
            _connection = connection;
        }
        //protected member Logging class that inherits from QueuedSubscriber
        protected EventStoreMessageLogger Logging;
        protected string StreamName = $"LogTest-{Guid.NewGuid():N}";
        protected GetEventStoreRepository Repo;
        protected override void Given()
        {
            Repo = new GetEventStoreRepository(_connection);
            // ctor defaults to disabled
            Logging = new EventStoreMessageLogger(Bus,
                _connection,
                StreamName);

            Thread.Sleep(1000);
        }
    }
}
