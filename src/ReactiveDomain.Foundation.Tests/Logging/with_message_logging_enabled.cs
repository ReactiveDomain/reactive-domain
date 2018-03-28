using System;
using System.Threading;
using EventStore.ClientAPI;
using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging.Testing;

namespace ReactiveDomain.Foundation.Tests.Logging
{
    // ReSharper disable once InconsistentNaming
    public abstract class with_message_logging_enabled :CommandBusSpecification
    {
        protected readonly IEventStoreConnection Connection;

        protected with_message_logging_enabled(IEventStoreConnection connection)
        {
            Connection = connection;
        }
        protected EventStoreMessageLogger Logging;
        protected string StreamName = $"LogTest-{Guid.NewGuid():N}";
        protected EventStoreRepository Repo;
        protected PrefixedCamelCaseStreamNameBuilder StreamNameBuilder;
        protected override void Given()
        {
            StreamNameBuilder = new PrefixedCamelCaseStreamNameBuilder("UnitTest");
            Repo = new EventStoreRepository(StreamNameBuilder, Connection);
            // instantiate Logger class that inherits from QueuedSubscriber
            Logging = new EventStoreMessageLogger(Bus,
                Connection,
                StreamName,
                true);

            Thread.Sleep(2000); // needs a bit of time to set up the ES
        }
    }
}
