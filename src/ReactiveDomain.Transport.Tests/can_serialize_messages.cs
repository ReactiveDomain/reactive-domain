using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Transport.Serialization;
using Xunit;

namespace ReactiveDomain.Transport.Tests
{
    // ReSharper disable InconsistentNaming
    public class when_creating_tcp_messages
    {
        private const string Prop1 = "prop1";
        private const string Prop2 = "prop2";
        private readonly WoftamEvent _testEvent = new WoftamEvent(Prop1, Prop2);

        [Fact]
        public void can_serialize_from_message()
        {
            var serializer = new SimpleJsonSerializer();
            var json = serializer.SerializeMessage(_testEvent);
            var encoder = new TcpMessageEncoder();
            var data = encoder.ToBytes(json, _testEvent.GetType());
            var decoded = encoder.FromBytes(data);
            var msg = serializer.DeserializeMessage(decoded.Item1, decoded.Item2);
            Assert.IsType<WoftamEvent>(msg);
            var msg2 = msg as WoftamEvent;
            Assert.NotNull(msg2);
            // ReSharper disable once PossibleNullReferenceException
            Assert.Equal(_testEvent.Property1, msg2.Property1);
            Assert.Equal(_testEvent.Property2, msg2.Property2);
        }
    }

    public class WoftamEvent : IMessage
    {
        public Guid MsgId { get; }
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
