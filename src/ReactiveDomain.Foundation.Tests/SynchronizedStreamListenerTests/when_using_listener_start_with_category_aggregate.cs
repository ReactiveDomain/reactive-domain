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
    public class when_using_listener_start_with_category_aggregate
    {
        private readonly IEventSerializer _eventSerializer = new JsonMessageSerializer();

        public when_using_listener_start_with_category_aggregate(StreamStoreConnectionFixture fixture)
        {
            var streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder();
            var conn = fixture.Connection;
            conn.Connect();

            var aggStream = streamNameBuilder.GenerateForAggregate(typeof(AggregateCategoryTestAggregate), Guid.NewGuid());
            var categoryStream = streamNameBuilder.GenerateForCategory(typeof(AggregateCategoryTestAggregate));

            // Drop an event into the stream testAggregate-guid
            var result = conn.AppendToStream(
                aggStream, 
                ExpectedVersion.NoStream, 
                null, 
                _eventSerializer.Serialize(new TestEvent()));
            Assert.True(result.NextExpectedVersion == 0);

            //wait for the projection to be written.
            CommonHelpers.WaitForStream(conn, categoryStream);
            
            // Now set up the projection listener, and start it. 
            var listener = new QueuedStreamListener(
                "category listener",
                conn,
               streamNameBuilder,
                new JsonMessageSerializer());
            listener.EventStream.Subscribe<TestEvent>(new AdHocHandler<TestEvent>(Handle));
            listener.Start<AggregateCategoryTestAggregate>();
        }

        private long _testEventCount;
        [Fact]
        public void can_get_events_from_category_projection() {
            AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _testEventCount) == 1, 4000);
        }

        private void Handle(IMessage message) {
            dynamic evt = message;
            if (evt is TestEvent) {
                Interlocked.Increment(ref _testEventCount);
            }

        }
        public class AggregateCategoryTestAggregate:EventDrivenStateMachine{}
    }
}
