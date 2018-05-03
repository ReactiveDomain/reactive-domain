using System;
using System.Threading;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests.SynchronizedStreamListenerTests {
    // ReSharper disable once InconsistentNaming
    [Collection(nameof(EmbeddedStreamStoreConnectionCollection))]
    public class when_using_listener_start_with_category_aggregate
    {
        private class _testStreamNameBuilder:IStreamNameBuilder
        {
            private readonly Guid _testRunGuid;

            public _testStreamNameBuilder(Guid testRunGuid) {
                _testRunGuid = testRunGuid;
            }
            public string GenerateForAggregate(Type type, Guid id) {
                return $"{type.Name}{_testRunGuid:N}-{id:N}";
            }

            public string GenerateForCategory(Type type) {
                //mock category stream, can't use $ here
                return $"ce-{type.Name}{_testRunGuid:N}";
            }

            public string GenerateForEventType(string type) {
                //mock event type stream, can't use $ here
                return $"et-{type}{_testRunGuid:N}";
            }
        }

        private readonly IStreamNameBuilder _streamNameBuilder; 
        private readonly IEventSerializer _eventSerializer = new JsonMessageSerializer();
        private readonly string _categoryProjectionStream;
        private readonly IStreamStoreConnection _conn;

        public when_using_listener_start_with_category_aggregate(StreamStoreConnectionFixture fixture) {
            _streamNameBuilder = new _testStreamNameBuilder(Guid.NewGuid());
            _conn = fixture.Connection;
            _conn.Connect();

            _categoryProjectionStream = _streamNameBuilder.GenerateForCategory(typeof(TestAggregate));
            
            // Generate an array of events for the Append call
            var eventsToSave = CommonHelpers.GenerateEvents(_eventSerializer);

            // Drop the event into the stream
            var result = _conn.AppendToStream(_categoryProjectionStream, ExpectedVersion.NoStream, null, eventsToSave);
            Assert.True(result.NextExpectedVersion == 0);
            
            // Now set up the projection listener, and start it
            var listener = new SynchronizableStreamListener(
                "category listener",
                _conn,
               _streamNameBuilder,
                new JsonMessageSerializer());
            listener.EventStream.Subscribe(new AdHocHandler<Event>(Handle));
            listener.Start<TestAggregate>();
        }

        private long _testEventCount;
        [Fact]
        public void can_get_events_from_category_projection() {
            AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _testEventCount) == 1,3000);
        }

        public void Handle(Message message) {
            dynamic evt = message;
            if (evt is TestEvent) {
                Interlocked.Increment(ref _testEventCount);
            }

        }
    }
}
