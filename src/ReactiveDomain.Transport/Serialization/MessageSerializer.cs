namespace ReactiveDomain.Transport.Serialization
{
    using System;
    using System.Text;

    using Microsoft.Extensions.Logging;

    using Newtonsoft.Json;
    using ReactiveDomain.Messaging;
    using ReactiveDomain.Util;

    public class MessageSerializer : IMessageSerializer {

        private static readonly ILogger Log = Logging.LogProvider.GetLogger("ReactiveDomain");
        private static readonly Encoding Encoding = Helper.UTF8NoBom;

        public IMessage FromBytes(ArraySegment<byte> data) {
            if (data.Array == null || data.Count < sizeof(int))
                throw new ArgumentException($"ArraySegment null or too short, length: {data.Count}", nameof(data));

            var offset = data.Offset;
            offset += ReadBytes(data.Array, offset, out var typeNameByteCount);
            offset += ReadBytes(data.Array, offset, typeNameByteCount, out var typeName);
            offset += ReadBytes(data.Array, offset, out var jsonByteCount);
            ReadBytes(data.Array, offset, jsonByteCount, out var json);

            var messageType = MessageHierarchy.GetTypeByFullName(typeName);
            var msg = DeserializeMessage(json, messageType);

            Log.LogDebug("Deserialized Message MsgId=" + msg.MsgId + " MsgType" + msg.GetType().Name);
            return msg;
        }

        public ArraySegment<byte> ToBytes(IMessage message) {
            Ensure.NotNull(message, nameof(message));
            var messageType = message.GetType();
            Log.LogDebug("Message MsgId=" + message.MsgId + " MsgTypeId=" + messageType.Name + " to be wrapped.");

            var typeName = messageType.FullName;
            var json = SerializeMessage(message);

            var typeNameByteCount = Encoding.GetByteCount(typeName);
            var jsonByteCount = Encoding.GetByteCount(json);
            var totalByteCount = sizeof(int) + typeNameByteCount + sizeof(int) + jsonByteCount;

            var array = new byte[totalByteCount];
            var data = new ArraySegment<byte>(array);

            var offset = 0;
            offset += WriteBytes(typeNameByteCount, array, offset);
            offset += WriteBytes(typeName, array, offset);
            offset += WriteBytes(jsonByteCount, array, offset);
            WriteBytes(json, array, offset);

            return data;
        }

        protected virtual IMessage DeserializeMessage(string json, Type messageType) => (IMessage)JsonConvert.DeserializeObject(json, messageType, Json.JsonSettings);

        protected virtual string SerializeMessage(IMessage message) => JsonConvert.SerializeObject(message, Json.JsonSettings);

        protected static int ReadBytes(byte[] source, int offset, out int destination) {
            destination = BitConverter.ToInt32(source, offset);
            return sizeof(int);
        }

        protected static int WriteBytes(int source, byte[] destination, int offset) {
            destination[offset + 0] = (byte)source;
            destination[offset + 1] = (byte)(source >> 8);
            destination[offset + 2] = (byte)(source >> 16);
            destination[offset + 3] = (byte)(source >> 24);
            return sizeof(int);
        }

        protected static int ReadBytes(byte[] source, int offset, int count, out string destination) {
            destination = Encoding.GetString(source, offset, count);
            return count;
        }

        protected static int WriteBytes(string source, byte[] destination, int offset) {
            return Encoding.GetBytes(source, 0, source.Length, destination, offset);
        }
    }
}