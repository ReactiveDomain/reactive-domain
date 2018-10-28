using System.IO;
using Newtonsoft.Json;
using ReactiveDomain.Messaging;
using ReactiveDomain.Transport.CommandSocket;
using Xunit;

namespace ReactiveDomain.Transport.Tests
{
    // ReSharper disable InconsistentNaming
    public class when_creating_tcp_messages
    {
        [Fact]
        public void can_serialize_from_message()
        {
           
            var tcpMsg = new TcpMessage(_testEvent);

            var tcpmsg2 = TcpMessage.FromArraySegment(tcpMsg.Data);
            Assert.IsType<WoftamEvent>(tcpmsg2.WrappedMessage);
            var msg2 = tcpmsg2.WrappedMessage as WoftamEvent;
            Assert.NotNull(msg2);
            // ReSharper disable once PossibleNullReferenceException
            Assert.Equal(_testEvent.Property1, msg2.Property1);
            Assert.Equal(_testEvent.Property1, ((WoftamEvent)tcpmsg2.WrappedMessage).Property1);
        }

        private const string Prop1 = "prop1";
        private const string Prop2 = "prop2";
        private readonly WoftamEvent _testEvent =  new WoftamEvent(Prop1,Prop2);
    }
    public class WoftamEvent : CorrelatedMessage
    {
   
        public WoftamEvent(string property1, string property2): base(CorrelationId.NewId(), SourceId.NullSourceId())
        {
            Property1 = property1;
            Property2 = property2;
        }

        public string Property1 { get; private set; }
        public string Property2 { get; private set; }
    }
}
