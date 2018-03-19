using System;
using System.Threading;
using EventStore.ClientAPI;
using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging.Testing;

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
        protected EventStoreRepository Repo;
        protected PrefixedCamelCaseStreamNameBuilder StreamNameBuilder;
        protected EventStoreCatchupStreamSubscriber Subscriber;
        protected override void Given()
        {
            StreamNameBuilder = new PrefixedCamelCaseStreamNameBuilder("UnitTest");
            Repo = new EventStoreRepository(new PrefixedCamelCaseStreamNameBuilder("UnitTest"),_connection);
            // ctor defaults to disabled
            Logging = new EventStoreMessageLogger(Bus,
                _connection,
                StreamName);
            Subscriber = new EventStoreCatchupStreamSubscriber(_connection);

            Thread.Sleep(1000);
        }
    }
}
