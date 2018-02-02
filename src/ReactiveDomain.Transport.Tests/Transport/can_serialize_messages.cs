using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Bson;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Messages;
using ReactiveDomain.Messaging.Testing;
using ReactiveDomain.Transport.CommandSocket;
using Xunit;

namespace ReactiveDomain.Transport.Tests.Transport
{
    public class when_creating_tcp_messages
    {
        [Fact]
        public void can_serialize_from_message()
        {
            var msg = new WoftamEvent("test1", "test2");
            var tcpMsg = new TcpMessage(msg);
            var tcpmsg2 = TcpMessage.FromArraySegment(tcpMsg.Data);
            Assert.IsType<WoftamEvent>(tcpmsg2.WrappedMessage);
            var msg2 = tcpmsg2.WrappedMessage as WoftamEvent;
            Assert.Equal(msg.Property1, msg2.Property1);
        }

        [Fact]
        public void bson_test()
        {
            MemoryStream ms = new MemoryStream();
            BsonWriter bs = new BsonWriter(ms);
            bs.WriteStartObject();
            bs.WritePropertyName("Value");
            bs.WriteValue("Dummy");
            bs.WriteEnd();
 
            ms.Seek(0, SeekOrigin.Begin);
 
            BsonReader reader = new BsonReader(ms);
            // object
            reader.Read();
            // property name
            reader.Read();
            Assert.Equal("Value", (string) reader.Value);
        }

    }
    public class WoftamEvent : Message, ICorrelatedMessage
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;
        public WoftamEvent(string property1, string property2)
        {
            Property1 = property1;
            Property2 = property2;
        }

        public string Property1 { get; private set; }
        public string Property2 { get; private set; }

        #region Implementation of ICorrelatedMessage
        public Guid? SourceId => null;
        public Guid CorrelationId => Guid.Empty;
        #endregion
    }
}
