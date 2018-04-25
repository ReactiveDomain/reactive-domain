using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using System.Threading;
using Xunit;

namespace ReactiveDomain.Foundation.Tests.Logging
{

    // ReSharper disable once InconsistentNaming
    [Collection(nameof(EmbeddedStreamStoreConnectionCollection))]
    public class when_events_are_published : 
        with_message_logging_enabled,
        IHandle<Event>
    {
        static when_events_are_published()
        {
            BootStrap.Load();
        }
        public when_events_are_published(StreamStoreConnectionFixture fixture):base(fixture.Connection)
        {
            _listener = new SynchronizableStreamListener(
                Logging.FullStreamName, 
                Connection, 
                StreamNameBuilder,
                EventSerializer);
            _listener.EventStream.Subscribe<Event>(this);

            _listener.Start(Logging.FullStreamName);

            _countedEventCount = 0;
            _testDomainEventCount = 0;
            CorrelatedMessage source = CorrelatedMessage.NewRoot();
            // create and publish a set of events
            for (int i = 0; i < _maxCountedEvents; i++)
            {
                var evt = new CountedEvent(i, source);
                Bus.Publish(evt);
                source = evt;
            }

            Bus.Subscribe(new AdHocHandler<TestEvent>(_ => Interlocked.Increment(ref _gotEvt)));
            Bus.Publish(new TestEvent(source));
        }
       
        private IListener _listener;

        private readonly int _maxCountedEvents = 5;
        private int _countedEventCount;
        private int _testDomainEventCount;
        private long _gotEvt;
       
        [Fact(Skip="To be fixed")]
        public void all_events_are_logged()
        {
            Assert.IsOrBecomesTrue(()=> _gotEvt >0);

            // Wait  for last event to be queued
            Assert.IsOrBecomesTrue(()=>_countedEventCount == _maxCountedEvents, 2000);
            Assert.True(_countedEventCount == _maxCountedEvents, $"Message {_countedEventCount} doesn't match expected index {_maxCountedEvents}");
            Assert.IsOrBecomesTrue(() => _testDomainEventCount == 1, 1000);

            Assert.True(_testDomainEventCount == 1, $"Last event count {_testDomainEventCount} doesn't match expected value {1}");
        }
       
        public void Handle(Event message)
        {
            if (message is CountedEvent) _countedEventCount++;
            if (message is TestEvent) _testDomainEventCount++;
        }
    }
}
