using System;
using System.Threading;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests.SynchronizedStreamListenerTests {
    // ReSharper disable once InconsistentNaming
    [Collection(nameof(EmbeddedStreamStoreConnectionCollection))]
    public class when_using_listener_start_with_custom_stream_synched_bus
    {
        private readonly IStreamNameBuilder _streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder();
        private readonly IEventSerializer _eventSerializer = new JsonMessageSerializer();
        private readonly string _originStreamName;
        private readonly string _categoryProjectionStream;
        private readonly IStreamStoreConnection _conn;

        public when_using_listener_start_with_custom_stream_synched_bus(StreamStoreConnectionFixture fixture)
        {
            _conn = fixture.Connection;
            _conn.Connect();

            // Build an origin stream from strings to which the the events are appended
            _originStreamName = $"testStream-{Guid.NewGuid():N}";

            // Generate event and save it to the custom stream
            var eventsToSave = CommonHelpers.GenerateEvents(_eventSerializer);
            var result = fixture.Connection.AppendToStream(_originStreamName, ExpectedVersion.NoStream, null, eventsToSave);
            Assert.True(result.NextExpectedVersion == 0);

            // Wait for the stream to be written
            CommonHelpers.WaitForStream(_conn, _originStreamName);

            StreamListener listener = new SynchronizableStreamListener(
                _originStreamName,
                fixture.Connection,
                new PrefixedCamelCaseStreamNameBuilder(),
                _eventSerializer,
                true,
                "BUS_NAME");
            listener.EventStream.Subscribe(new AdHocHandler<Event>(Handle));
            listener.Start(_originStreamName);
        }

        private long _testEventCount;

        [Fact]
        public void can_get_events_from_category_stream() 
        {
            AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _testEventCount) == 1, 3000);
        }

        public void Handle(Message message)
        {
            dynamic evt = message;
            if (evt is TestEvent)
            {
                Interlocked.Increment(ref _testEventCount);
            }
        }
    }
}
