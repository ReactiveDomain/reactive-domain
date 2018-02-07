using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Transport.CommandSocket
{
    public struct TcpMessage
    {
        public const int MsgIdOffset = 0;

        public readonly int MessageId;
        public readonly Type MessageType;
        public readonly ArraySegment<byte> Data;
        public readonly Message WrappedMessage;

        public static TcpMessage FromArraySegment(ArraySegment<byte> data)
        {
            var d = data.Array;
            if (data.Count < sizeof(int))
                throw new ArgumentException(string.Format("ArraySegment too short, length: {0}", data.Count), "data");
            var msgId = 0;
            for (var i = 0; i < 4; i++)
            {
                msgId |= (d[i] << (i * 8)); // little-endian order
            }
            //TODO: CLC Replace this with more robust lookup, Different version of system can have different ids for messages and this will not survive persistance
            var msgType = MessageHierarchy.GetMsgType(msgId);

            var messageBuffer = new byte[d.Length - sizeof(int)];
            Buffer.BlockCopy(data.Array, data.Offset + sizeof(int), messageBuffer, 0, messageBuffer.Length);
            var ms = new MemoryStream(messageBuffer);
            Message msg;
            using (var reader = new BsonDataReader(ms))
            {
                var serializer = new JsonSerializer();
                msg = (Message)serializer.Deserialize(reader, msgType);
            }
            return new TcpMessage(msg);
        }

        public TcpMessage(Message message)
        {
            //Debug.WriteLine("Message MsgId=" + message.MsgId + " MsgTypeId=" + message.MsgTypeId + " to be wrapped.");
            MessageId = message.MsgTypeId;
            MessageType = message.GetType();
            var ms = new MemoryStream();
            using (var writer = new BsonDataWriter(ms))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(writer, message, MessageType);
            }
            Data = new ArraySegment<byte>(ms.ToArray());
            WrappedMessage = message;

            //var ms2 = new MemoryStream(Data.Array);
            //Message msg;
            //using (var reader = new BsonReader(ms2))
            //{
            //    var serializer = new JsonSerializer();
            //    msg = (Message)serializer.Deserialize(reader, MessageType);
            //}
            //Debug.WriteLine("Deserialized Message MsgId=" + msg.MsgId + " MsgTypeId=" + msg.MsgTypeId);
        }

        public byte[] AsByteArray()
        {
            var res = new byte[sizeof(int) + Data.Count];
            res[MsgIdOffset] = (byte)MessageId;
            res[MsgIdOffset + 1] = (byte)(MessageId >> 8);
            res[MsgIdOffset + 2] = (byte)(MessageId >> 16);
            res[MsgIdOffset + 3] = (byte)(MessageId >> 24);
            Buffer.BlockCopy(Data.Array, Data.Offset, res, res.Length - Data.Count, Data.Count);
            return res;
        }

        public ArraySegment<byte> AsArraySegment()
        {
            return new ArraySegment<byte>(AsByteArray());
        }
    }

}
