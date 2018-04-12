using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Messages;
using ReactiveDomain.Transport.CommandSocket;
using Xunit;

namespace ReactiveDomain.Transport.Tests
{
    // ReSharper disable InconsistentNaming
    public class when_creating_tcp_messages
    {
        [Fact]
        public void bson_serialize_object_test()
        {
            var propName = "Value";
            var propValue = "Dummy";
            MemoryStream ms = new MemoryStream();
            var bs = new BsonDataWriter(ms);
            bs.WriteStartObject();
            bs.WritePropertyName(propName);
            bs.WriteValue(propValue);
            bs.WriteEnd();
 
            ms.Seek(0, SeekOrigin.Begin);
 
            var reader = new BsonDataReader(ms);
            // object
            reader.Read();
            // property name
            reader.Read();
            Assert.Equal(propName, (string) reader.Value);
            reader.Read();
            Assert.Equal(propValue, (string) reader.Value);
        }
        [Fact]
        public void bson_serialize_message_test()
        {
            var prop1 = "prop1";
            var prop2 = "prop2";
            var msg = new WoftamEvent(prop1,prop2);
            MemoryStream ms = new MemoryStream();
            var bs = new BsonDataWriter(ms);
            bs.WriteStartObject();
            bs.WritePropertyName(msg.GetType().FullName);
            bs.WriteValue(JsonConvert.SerializeObject(msg));
            bs.WriteEnd();
 
            ms.Seek(0, SeekOrigin.Begin);
            Message msg2;
            var reader = new BsonDataReader(ms);
            // read object
            reader.Read();
            // read type name
            reader.Read();
            var messageType = MessageHierarchy.GetMsgType((string)reader.Value);
            reader.Read(); //property value
            msg2 = (Message)JsonConvert.DeserializeObject((string)reader.Value, messageType);
            Assert.IsType<WoftamEvent>(msg2);
            Assert.Equal(prop1,((WoftamEvent)msg2).Property1);
        }

        [Fact]
        public void can_create_tcp_message_from_message()
        {
            var tcpMsg = new TcpMessage(_testEvent);
            Assert.NotNull(tcpMsg.Data);
            // ReSharper disable once AssignNullToNotNullAttribute
            var reader = new BsonDataReader(new MemoryStream(tcpMsg.Data.Array));
            // read object
            reader.Read();
            // read type name
            reader.Read();
            var messageType = MessageHierarchy.GetMsgType((string)reader.Value);
            reader.Read(); //read json value
            var msg2 = (Message)JsonConvert.DeserializeObject((string)reader.Value, messageType, Json.JsonSettings);
            Assert.IsType<WoftamEvent>(msg2);
            Assert.Equal(Prop1,((WoftamEvent)msg2).Property1);
        }

        [Fact]
        public void can_create_tcp_message_from_byte_array()
        {
            var tcpMsg = new TcpMessage(_testEvent);
            Assert.NotNull(tcpMsg.Data);
            // ReSharper disable once AssignNullToNotNullAttribute
            var reader = new BsonDataReader(new MemoryStream(tcpMsg.Data.Array));
            // read object
            reader.Read();
            // read type name
            reader.Read();
            var messageType = MessageHierarchy.GetMsgType((string)reader.Value);
            reader.Read(); //read json value
            var msg2 = (Message)JsonConvert.DeserializeObject((string)reader.Value, messageType, Json.JsonSettings);
            Assert.IsType<WoftamEvent>(msg2);
            Assert.Equal(Prop1,((WoftamEvent)msg2).Property1);
        }

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
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;
        public WoftamEvent(string property1, string property2): base(CorrelationId.NewId(), SourceId.NullSourceId())
        {
            Property1 = property1;
            Property2 = property2;
        }

        public string Property1 { get; private set; }
        public string Property2 { get; private set; }
    }
}
