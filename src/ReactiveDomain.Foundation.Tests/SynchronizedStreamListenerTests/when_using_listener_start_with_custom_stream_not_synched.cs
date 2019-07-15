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
    public class when_using_listener_start_with_custom_stream_not_synched
    {
        private readonly IStreamNameBuilder _streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder();
        private readonly IEventSerializer _eventSerializer = new JsonMessageSerializer();

        public when_using_listener_start_with_custom_stream_not_synched(StreamStoreConnectionFixture fixture)
        {
            var conn = fixture.Connection;
            conn.Connect();

            // Build an origin stream from strings to which the the events are appended
            var originStreamName = $"testStream-{Guid.NewGuid():N}";

            // Generate event and save it to the custom stream
            
            var result = fixture.Connection.AppendToStream(
                                               originStreamName, 
                                               ExpectedVersion.NoStream, 
                                               null, 
                                               _eventSerializer.Serialize(new TestEvent()));
            Assert.True(result.NextExpectedVersion == 0);

            // Wait for the stream to be written
            CommonHelpers.WaitForStream(conn, originStreamName);

            StreamListener listener = new QueuedStreamListener(
                originStreamName,
                fixture.Connection,
                new PrefixedCamelCaseStreamNameBuilder(),
                _eventSerializer);
            listener.EventStream.Subscribe(new AdHocHandler<Event>(Handle));
            listener.Start(originStreamName);
        }

        private long _testEventCount;

        [Fact]
        public void can_get_events_from_custom_stream() 
        {
            AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _testEventCount) == 1, 3000);
        }

        private void Handle(IMessage message)
        {
            dynamic evt = message;
            if (evt is TestEvent)
            {
                Interlocked.Increment(ref _testEventCount);
            }
        }
    }
}
