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
    public class when_using_listener_start_with_aggregate_and_guid
    {
        private readonly IStreamNameBuilder _streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder();
        private readonly IEventSerializer _eventSerializer = new JsonMessageSerializer();

        public when_using_listener_start_with_aggregate_and_guid(StreamStoreConnectionFixture fixture){
            var conn = fixture.Connection;
            conn.Connect();

            // Build an origin stream to which the the events are appended. We are testing this stream directly
            var originalStreamGuid = Guid.NewGuid();
            var originStreamName = _streamNameBuilder.GenerateForAggregate(typeof(AggregateIdTestAggregate), originalStreamGuid);

            
            // Drop an event into the stream testAggregate-guid
            var result = conn.AppendToStream(
                                originStreamName, 
                                ExpectedVersion.NoStream, 
                                null, 
                                _eventSerializer.Serialize(new TestEvent()));
            Assert.True(result.NextExpectedVersion == 0);

            // Wait for the stream to be written
            CommonHelpers.WaitForStream(conn, originStreamName);

            StreamListener listener = new QueuedStreamListener(
                "guidStream",
                conn,
                _streamNameBuilder,
                _eventSerializer);
            listener.EventStream.Subscribe(new AdHocHandler<Event>(Handle));

            // This will start listening on the TestAggregate-guid stream.
            listener.Start<AggregateIdTestAggregate>(originalStreamGuid);
        }

        private long _testEventCount;
        [Fact]
        public void can_get_events_from_aggregate_id_stream()
        {
            AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _testEventCount) == 1,3000);
        }

        private void Handle(IMessage message) {
            dynamic evt = message;
            if (evt is TestEvent) {
                Interlocked.Increment(ref _testEventCount);
            }

        }
        public class AggregateIdTestAggregate:EventDrivenStateMachine{}
    }
}
