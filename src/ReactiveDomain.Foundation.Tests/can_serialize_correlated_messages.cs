using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests {

    // ReSharper disable once InconsistentNaming
    public class when_serializing_correlated_messages {
        [Fact]
        public void can_use_json_message_serializer() {
           
            var evt = new TestEvent(CorrelatedMessage.NewRoot());
            var evt2 = new TestEvent(evt);

            var serializer = new JsonMessageSerializer();

            var data = serializer.Serialize(evt);
            var data2 = serializer.Serialize(evt2);

            var dEvent = (TestEvent)serializer.Deserialize(data);
            var dEvent2 = (TestEvent)serializer.Deserialize(data2);

            Assert.Equal(evt.MsgId,dEvent.MsgId);
            Assert.Equal(evt.SourceId,dEvent.SourceId);
            Assert.Equal(evt.CorrelationId,dEvent.CorrelationId);

            Assert.Equal(evt2.MsgId,dEvent2.MsgId);
            Assert.Equal(evt2.SourceId,dEvent2.SourceId);
            Assert.Equal(evt2.CorrelationId,dEvent2.CorrelationId);
        }
    }
}
