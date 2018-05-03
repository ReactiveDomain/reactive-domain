using System;
using System.Collections.Generic;
using System.Threading;
using EventStore.ClientAPI;
using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests.SynchronizedStreamListenerTests {
    // ReSharper disable once InconsistentNaming
    [Collection(nameof(EmbeddedStreamStoreConnectionCollection))]
    public class when_using_listener_start_with_event_type
    {
        private class TestStreamNameBuilder:IStreamNameBuilder
        {
            private readonly Guid _testRunGuid;

            public TestStreamNameBuilder(Guid testRunGuid) {
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
        
        private readonly string _eventProjectionStream;
        private readonly IStreamStoreConnection _conn;

        public when_using_listener_start_with_event_type(StreamStoreConnectionFixture fixture) {
            _streamNameBuilder = new TestStreamNameBuilder(Guid.NewGuid());
            _conn = fixture.Connection;
            _conn.Connect();
          
          
            _eventProjectionStream = _streamNameBuilder.GenerateForEventType(nameof(TestEvent));
            
            //wrap the event into an array for Append call
            var eventToSave = CommonHelpers.GenerateEvents(_eventSerializer);

            //drop the event into the stream
            var result = _conn.AppendToStream(_eventProjectionStream, ExpectedVersion.NoStream, null, eventToSave);
            Assert.True(result.NextExpectedVersion == 0);

            //wait for the projection to be written
            CommonHelpers.WaitForStream(_conn, _eventProjectionStream);

            //build the listener
            StreamListener listener = new SynchronizableStreamListener(
                "test listener",
                _conn,
                _streamNameBuilder,
                _eventSerializer);

            listener.EventStream.Subscribe(new AdHocHandler<Event>(Handle));
            listener.Start(typeof(TestEvent));
        }

        private long _testEventCount;
        [Fact]
        public void can_get_events_from_event_projection() {
            AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _testEventCount) == 1, 3000);
        }

        public void Handle(Message message) {
            dynamic evt = message;
            if (evt is TestEvent) {
                Interlocked.Increment(ref _testEventCount);
            }

        }
    }
}
