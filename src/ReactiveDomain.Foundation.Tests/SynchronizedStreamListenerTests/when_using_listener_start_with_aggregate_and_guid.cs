using System;
using System.Collections.Generic;
using System.Threading;
using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests.SynchronizedStreamListenerTests {
    // ReSharper disable once InconsistentNaming
    [Collection(nameof(EmbeddedStreamStoreConnectionCollection))]
    public class when_using_listener_start_with_aggregate_and_guid
    {
        private readonly IStreamNameBuilder _streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder();
        private readonly IEventSerializer _eventSerializer = new JsonMessageSerializer();
        private readonly string _originStreamName;
        private readonly IStreamStoreConnection _conn;

        public when_using_listener_start_with_aggregate_and_guid(StreamStoreConnectionFixture fixture){
            _conn = fixture.Connection;
            _conn.Connect();

            // Build an origin stream to which the the events are appended. We are testing this stream directly
            var originalStreamGuid = Guid.NewGuid();
            _originStreamName = _streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), originalStreamGuid);

            // Generate an array of events for the Append call
            var eventsToSave = CommonHelpers.GenerateEvents(_eventSerializer);

            // Drop the event into the stream testAggregate-guid
            var result = _conn.AppendToStream(_originStreamName, ExpectedVersion.NoStream, null, eventsToSave);
            Assert.True(result.NextExpectedVersion == 0);

            // Wait for the stream to be written
            CommonHelpers.WaitForStream(_conn, _originStreamName);

            StreamListener listener = new SynchronizableStreamListener(
                "guidStream",
                _conn,
                _streamNameBuilder,
                _eventSerializer);
            listener.EventStream.Subscribe(new AdHocHandler<Event>(Handle));

            // This will start listening on the TestAggregate-guid stream.
            listener.Start<TestAggregate>(originalStreamGuid);
        }

        private long _testEventCount;
        [Fact]
        public void can_get_events_from_category_stream()
        {
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _testEventCount) == 1,3000);
        }

        public void Handle(Message message) {
            dynamic evt = message;
            if (evt is TestEvent) {
                Interlocked.Increment(ref _testEventCount);
            }

        }
    }
}
