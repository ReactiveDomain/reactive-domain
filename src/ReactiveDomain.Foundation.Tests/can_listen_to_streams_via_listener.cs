using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests {
    // ReSharper disable once InconsistentNaming
    [Collection(nameof(EmbeddedStreamStoreConnectionCollection))]
    public class when_using_listener {
        public when_using_listener(StreamStoreConnectionFixture fixture) {
            //given
            fixture.Connection.Connect();
            var testStream = $"testStream-{Guid.NewGuid()}";
            IEventSerializer eventSerializer = new JsonMessageSerializer();

            StreamListener listener = new SynchronizableStreamListener(
                testStream,
                fixture.Connection,
                new PrefixedCamelCaseStreamNameBuilder(),
                eventSerializer);

            listener.EventStream.Subscribe(new AdHocHandler<Event>(Handle));

            //when
            var eventsToSave = new [] {
                eventSerializer.Serialize(
                    new TestEvent(CorrelatedMessage.NewRoot()),
                    new Dictionary<string, object>())};
           
            var result = fixture.Connection.AppendToStream(testStream, ExpectedVersion.NoStream, null, eventsToSave);
            Assert.True(result.NextExpectedVersion == 0);

            listener.Start(testStream);
        }

        private long _testEventCount;
        [Fact]
        public void can_get_events_from_stream() {
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
