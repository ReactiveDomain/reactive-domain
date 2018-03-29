using System;
using System.Threading;
using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging.Testing;

namespace ReactiveDomain.Foundation.Tests.Logging
{
    // ReSharper disable once InconsistentNaming
    public abstract class with_message_logging_disabled : CommandBusSpecification
    {
        protected readonly IStreamStoreConnection Connection;
        
        protected with_message_logging_disabled(IStreamStoreConnection connection)
        {
            Connection = connection;
        }
        //protected member Logging class that inherits from QueuedSubscriber
        protected EventStoreMessageLogger Logging;
        protected string StreamName = $"LogTest-{Guid.NewGuid():N}";
        protected EventStoreRepository Repo;
        protected IStreamNameBuilder StreamNameBuilder;
        protected override void Given()
        {
            StreamNameBuilder = new PrefixedCamelCaseStreamNameBuilder("UnitTest");
            Repo = new EventStoreRepository(StreamNameBuilder, Connection);
            // ctor defaults to disabled
            Logging = new EventStoreMessageLogger(Bus,
                Connection,
                StreamName);

            Thread.Sleep(1000);
        }
    }
}
