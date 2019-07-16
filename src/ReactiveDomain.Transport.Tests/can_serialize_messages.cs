using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Transport.Serialization;
using Xunit;

namespace ReactiveDomain.Transport.Tests
{
    // ReSharper disable InconsistentNaming
    public class when_creating_tcp_messages
    {
        [Fact]
        public void can_serialize_from_message()
        {
            var serializer = new MessageSerializer();
            var data = serializer.ToBytes(_testEvent);
            var tcpmsg2 = serializer.FromBytes(data);
            Assert.IsType<WoftamEvent>(tcpmsg2);
            var msg2 = tcpmsg2 as WoftamEvent;
            Assert.NotNull(msg2);
            // ReSharper disable once PossibleNullReferenceException
            Assert.Equal(_testEvent.Property1, msg2.Property1);
            Assert.Equal(_testEvent.Property1, ((WoftamEvent)tcpmsg2).Property1);
        }

        private const string Prop1 = "prop1";
        private const string Prop2 = "prop2";
        private readonly WoftamEvent _testEvent = new WoftamEvent(Prop1, Prop2);
    }

    public class WoftamEvent : IMessage
    {
        public Guid MsgId { get; private set; }
        public WoftamEvent(string property1, string property2)
        {
            MsgId = Guid.NewGuid();
            Property1 = property1;
            Property2 = property2;
        }

        public string Property1 { get; }
        public string Property2 { get; }


    }
}
