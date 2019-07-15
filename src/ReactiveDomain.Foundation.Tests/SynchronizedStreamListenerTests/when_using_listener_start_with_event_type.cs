using System;
using System.Threading;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using ReactiveDomain.Foundation.Tests.SynchronizedStreamListenerTests.Common;
using Xunit;

namespace ReactiveDomain.Foundation.Tests.SynchronizedStreamListenerTests {
    // ReSharper disable once InconsistentNaming
    [Collection(nameof(EmbeddedStreamStoreConnectionCollection))]
    public class when_using_listener_start_with_event_type
    {
        private readonly IEventSerializer _eventSerializer = new JsonMessageSerializer();

        public when_using_listener_start_with_event_type(StreamStoreConnectionFixture fixture) {
            var streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder();
            var conn = fixture.Connection;
            conn.Connect();

            var originalAggregateStream = 
                    streamNameBuilder.GenerateForAggregate(
                                            typeof(TestAggregate), 
                                            Guid.NewGuid());
            var evt = new EventProjectionTestEvent();
            //drop the event into the stream
            var result = conn.AppendToStream(
                                originalAggregateStream, 
                                ExpectedVersion.NoStream, 
                                null, 
                                _eventSerializer.Serialize(evt));
            Assert.True(result.NextExpectedVersion == 0);
            
            //wait for the projection to be written
            CommonHelpers.WaitForStream(conn, streamNameBuilder.GenerateForEventType(nameof(EventProjectionTestEvent)));

            //build the listener
            StreamListener listener = new QueuedStreamListener(
                "event listener",
                conn,
                streamNameBuilder,
                _eventSerializer);

            listener.EventStream.Subscribe(new AdHocHandler<EventProjectionTestEvent>(Handle));
            listener.Start(typeof(EventProjectionTestEvent));
        }

        private long _testEventCount;
        [Fact]
        public void can_get_events_from_event_type_stream() {
            AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _testEventCount) == 1, 4000,"Event Not Received");
        }

        private void Handle(IMessage message) {
            dynamic evt = message;
            if (evt is EventProjectionTestEvent) {
                Interlocked.Increment(ref _testEventCount);
            }

        }
        public class EventProjectionTestEvent : Event { }
    }
}
