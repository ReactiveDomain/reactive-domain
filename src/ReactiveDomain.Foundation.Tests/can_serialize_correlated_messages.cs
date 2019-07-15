using ReactiveDomain.Messaging;
using ReactiveDomain.Testing;
using System;
using Xunit;

namespace ReactiveDomain.Foundation.Tests {

    // ReSharper disable once InconsistentNaming
    public class when_serializing_correlated_messages {
        [Fact]
        public void can_use_json_message_serializer() {
           
            var evt = new TestEvent();
            var evt2 = MessageBuilder
                            .From(evt)
                            .Build(()=> new TestEvent());

            var serializer = new JsonMessageSerializer();

            var data = serializer.Serialize(evt);
            var data2 = serializer.Serialize(evt2);

            var dEvent = (TestEvent)serializer.Deserialize(data);
            var dEvent2 = (TestEvent)serializer.Deserialize(data2);

            Assert.Equal(evt.MsgId,dEvent.MsgId);
            Assert.Equal(evt.CausationId,dEvent.CausationId);
            Assert.Equal(evt.CorrelationId,dEvent.CorrelationId);

            Assert.Equal(evt2.MsgId,dEvent2.MsgId);
            Assert.Equal(evt2.CausationId,dEvent2.CausationId);
            Assert.Equal(evt2.CorrelationId, dEvent2.CorrelationId);
        }
        public class TestEvent : ICorrelatedMessage {
            public Guid MsgId { get; private set; }
            public TestEvent()
            {
                MsgId = Guid.NewGuid();
            }
            public Guid CorrelationId { get; set; }
            public Guid CausationId { get; set; }
        }
    }
}
