using System;
using System.Threading;
using EventStore.ClientAPI;
using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging.Tests.Specifications;

namespace ReactiveDomain.Foundation.Tests.Logging
{
    // ReSharper disable once InconsistentNaming
    public abstract class with_message_logging_enabled :CommandBusSpecification
    {
        private readonly IEventStoreConnection _connection;

        protected with_message_logging_enabled(IEventStoreConnection connection)
        {
            _connection = connection;
        }
        protected EventStoreMessageLogger Logging;
        protected string StreamName = $"LogTest-{Guid.NewGuid():N}";
        protected GetEventStoreRepository Repo;
        protected override void Given()
        {
            Repo = new GetEventStoreRepository(_connection);
            // instantiate Logger class that inherits from QueuedSubscriber
            Logging = new EventStoreMessageLogger(Bus,
                _connection,
                StreamName,
                true);

            Thread.Sleep(2000); // needs a bit of time to set up the ES
        }
    }
}
