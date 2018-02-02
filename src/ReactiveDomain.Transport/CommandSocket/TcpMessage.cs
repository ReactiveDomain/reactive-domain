using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Logging;

namespace ReactiveDomain.Transport.CommandSocket
{
    public struct TcpMessage
    {
        static readonly ILogger Log = LogManager.GetLogger("ReactiveDomain");
        public const int MsgIdOffset = 0;

        public readonly int MessageId;
        public readonly Type MessageType;
        public readonly ArraySegment<byte> Data;
        public readonly Message WrappedMessage;

        public static TcpMessage FromArraySegment(ArraySegment<byte> data)
        {

            if (data.Array == null || data.Count < sizeof(int))
                throw new ArgumentException($"ArraySegment null or too short, length: {data.Count}", nameof(data));

            var ms = new MemoryStream(data.Array);
            Message msg;
            using (var reader = new BsonReader(ms))
            {
                reader.Read(); //object
                reader.Read(); //property name

                var messageType = MessageHierarchy.GetMsgType((string)reader.Value);
                reader.Read(); //property value
                msg = (Message)JsonConvert.DeserializeObject((string)reader.Value, messageType);
            }
            Log.Debug("Deserialized Message MsgId=" + msg.MsgId + " MsgTypeId=" + msg.MsgTypeId);
            return new TcpMessage(msg);
        }

        public TcpMessage(Message message)
        {
            MessageId = message.MsgTypeId;
            MessageType = message.GetType();
            Log.Debug("Message MsgId=" + message.MsgId + " MsgTypeId=" + message.MsgTypeId + " to be wrapped.");

            var ms = new MemoryStream();
            using (var writer = new BsonWriter(ms))
            {
                writer.WriteStartObject();
                writer.WritePropertyName(MessageType.FullName);
                writer.WriteValue(JsonConvert.SerializeObject(message));
                writer.WriteEnd();
            }
            Data = new ArraySegment<byte>(ms.ToArray());
            WrappedMessage = message;
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
