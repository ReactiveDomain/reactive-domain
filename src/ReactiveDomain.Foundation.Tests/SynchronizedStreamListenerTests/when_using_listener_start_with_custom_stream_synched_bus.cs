using System;
using System.Threading;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using ReactiveDomain.Foundation.Tests.SynchronizedStreamListenerTests.Common;
using Xunit;
using ReactiveDomain.Util;

namespace ReactiveDomain.Foundation.Tests.SynchronizedStreamListenerTests {
    // ReSharper disable once InconsistentNaming
    [Collection(nameof(EmbeddedStreamStoreConnectionCollection))]
    public class when_using_listener_start_with_custom_stream_synched_bus
    {
        private readonly IStreamNameBuilder _streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder();
        private readonly IEventSerializer _eventSerializer = new JsonMessageSerializer();
        IStreamStoreConnection conn;
        StreamListener listener;
        IDisposable SubscriptionDisposer;

        public when_using_listener_start_with_custom_stream_synched_bus(StreamStoreConnectionFixture fixture)
        {
            conn = fixture.Connection;
            conn.Connect();

            // Build an origin stream from strings to which the the events are appended
            var originStreamName = $"testStream-{Guid.NewGuid():N}";

            var result = fixture.Connection.AppendToStream(
                                                originStreamName, 
                                                ExpectedVersion.NoStream, 
                                                null, 
                                                _eventSerializer.Serialize(new TestEvent()));
            Assert.True(result.NextExpectedVersion == 0);

            // Wait for the stream to be written
            CommonHelpers.WaitForStream(conn, originStreamName);

            listener = new QueuedStreamListener(
                originStreamName,
                fixture.Connection,
                new PrefixedCamelCaseStreamNameBuilder(),
                _eventSerializer,
                "BUS_NAME",
                LiveProcessingStarted);
            SubscriptionDisposer = listener.EventStream.Subscribe(new AdHocHandler<Event>(Handle));
            listener.Start(originStreamName);
        }

       

        private long _testEventCount;
        private long _gotLiveStarted;
        
        private void LiveProcessingStarted(Unit _) {
            Interlocked.Increment(ref _gotLiveStarted);
        }      
        [Fact]
        public void can_get_events_from_custom_stream() 
        {
            AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _testEventCount) == 1, 3000);
            AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _gotLiveStarted) == 1);        
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
