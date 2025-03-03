using System;
using System.Threading;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;
using ReactiveDomain.Foundation.Tests.StreamListenerTests.Common;

namespace ReactiveDomain.Foundation.Tests.StreamListenerTests
{
    // ReSharper disable once InconsistentNaming
    [Collection(nameof(EmbeddedStreamStoreConnectionCollection))]
    public class when_using_listener_start_with_future_stream
    {
        private readonly IStreamNameBuilder _streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder();
        private readonly IEventSerializer _eventSerializer = new JsonMessageSerializer();
        private readonly string _originStreamName = $"testStream-{Guid.NewGuid():N}";
        private readonly IStreamStoreConnection _connection;
        public when_using_listener_start_with_future_stream(StreamStoreConnectionFixture fixture)
        {
            _connection = fixture.Connection;
           

            StreamListener listener = new QueuedStreamListener(
                _originStreamName,
                fixture.Connection,
                new PrefixedCamelCaseStreamNameBuilder(),
                _eventSerializer);
            listener.EventStream.Subscribe(new AdHocHandler<Event>(Handle));
            listener.Start(_originStreamName);
        }

        private long _testEventCount;

        [Fact]
        public void validation_throws_on_mising_stream()
        {
            var missingStream = _originStreamName + "missing";
            StreamListener listener = new QueuedStreamListener(
                   missingStream,
                   _connection,
                   new PrefixedCamelCaseStreamNameBuilder(),
                   _eventSerializer);
            listener.EventStream.Subscribe(new AdHocHandler<Event>(Handle));
            Assert.Throws<ArgumentException>(() => listener.Start(missingStream, validateStream: true, cancelWaitToken: TestContext.Current.CancellationToken));
            listener.Dispose();
        }
        [Fact]
        public void can_subscribe_to_missing_stream()
        {
            var missingStream = _originStreamName + "missing";
            StreamListener listener = new QueuedStreamListener(
                   missingStream,
                   _connection,
                   new PrefixedCamelCaseStreamNameBuilder(),
                   _eventSerializer);
            listener.EventStream.Subscribe(new AdHocHandler<Event>(Handle));
            listener.Start(missingStream, validateStream: false, cancelWaitToken: TestContext.Current.CancellationToken);
            Assert.True(listener.IsLive);
            listener.Dispose();
        }

        [Fact]
        public void can_get_events_from_future_stream()
        {
            _connection.Connect();
            var result = _connection.AppendToStream(
                                               _originStreamName,
                                               ExpectedVersion.NoStream,
                                               null,
                                               _eventSerializer.Serialize(new TestEvent()));
            Assert.True(result.NextExpectedVersion == 0);

            // Wait for the stream to be written
            CommonHelpers.WaitForStream(_connection, _originStreamName);
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
