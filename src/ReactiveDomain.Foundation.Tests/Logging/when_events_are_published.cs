using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Testing;
using ReactiveDomain.Testing;
using System;
using System.Threading;
using Xunit;

namespace ReactiveDomain.Foundation.Tests.Logging
{

    // ReSharper disable once InconsistentNaming
    [Collection(nameof(EmbeddedStreamStoreConnectionCollection))]
    public class when_events_are_published : 
        with_message_logging_enabled,
        IHandle<DomainEvent>
    {
        static when_events_are_published()
        {
            BootStrap.Load();
        }
        public when_events_are_published(StreamStoreConnectionFixture fixture):base(fixture.Connection)
        {
            _listener = new SynchronizableStreamListener(Logging.FullStreamName, Connection, StreamNameBuilder);
            _listener.EventStream.Subscribe<DomainEvent>(this);

            _listener.Start(Logging.FullStreamName);

            _countedEventCount = 0;
            _testDomainEventCount = 0;

            // create and publish a set of events
            for (int i = 0; i < _maxCountedEvents; i++)
            {
                Bus.Publish(
                    new CountedEvent(i,
                        _correlationId,
                        Guid.NewGuid()));
            }

            Bus.Subscribe(new AdHocHandler<TestEvent>(_ => Interlocked.Increment(ref _gotEvt)));
            Bus.Publish(new TestEvent(_correlationId, Guid.NewGuid()));
        }
        private readonly Guid _correlationId = Guid.NewGuid();
        private IListener _listener;

        private readonly int _maxCountedEvents = 5;
        private int _countedEventCount;
        private int _testDomainEventCount;
        private long _gotEvt;
       
        
        public void all_events_are_logged()
        {
            Assert.IsOrBecomesTrue(()=> _gotEvt >0);

            // Wait  for last event to be queued
            Assert.IsOrBecomesTrue(()=>_countedEventCount == _maxCountedEvents, 9000);
            Assert.True(_countedEventCount == _maxCountedEvents, $"Message {_countedEventCount} doesn't match expected index {_maxCountedEvents}");
            Assert.IsOrBecomesTrue(() => _testDomainEventCount == 1, 1000);

            Assert.True(_testDomainEventCount == 1, $"Last event count {_testDomainEventCount} doesn't match expected value {1}");
        }

        public void Handle(DomainEvent message)
        {
            if (message is CountedEvent) _countedEventCount++;
            if (message is TestEvent) _testDomainEventCount++;
        }
    }
}
