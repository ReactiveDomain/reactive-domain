using System;
using System.Text;
using Newtonsoft.Json;
using ReactiveDomain.Messaging;
using ReactiveDomain.Logging;
using ReactiveDomain.Util;
using Settings = ReactiveDomain.Messaging.Json;

namespace ReactiveDomain.Transport.CommandSocket
{
    public struct TcpMessage
    {
        private static readonly ILogger Log = LogManager.GetLogger("ReactiveDomain");
        private static readonly Encoding Encoding = Helper.UTF8NoBom;
        public readonly Type MessageType;
        public readonly ArraySegment<byte> Data;
        public readonly Message WrappedMessage;

        /// <summary>
        /// Builds a tcp messages from a wrapped data array
        /// used by the tcp processing system to deserialize recieved data
        /// </summary>
        /// <param name="data">The wrapped memory array.</param>
        /// <returns>a new tcp message built from the array segment</returns>
        /// <exception cref="UnregisteredMessageException"> If the message type is not found this will throw UnregisteredMessageException</exception>
        public static TcpMessage FromArraySegment(ArraySegment<byte> data)
        {
            if (data.Array == null || data.Count < sizeof(int))
                throw new ArgumentException($"ArraySegment null or too short, length: {data.Count}", nameof(data));

            var offset = data.Offset;
            offset += ReadBytes(data.Array, offset, out var typeNameByteCount);
            offset += ReadBytes(data.Array, offset, typeNameByteCount, out var typeName);
            offset += ReadBytes(data.Array, offset, out var jsonByteCount);
            ReadBytes(data.Array, offset, jsonByteCount, out var json);

            var messageType = MessageHierarchy.GetTypeByFullName(typeName);
            var msg = (Message)JsonConvert.DeserializeObject(json, messageType, Settings.JsonSettings);

            Log.Debug("Deserialized Message MsgId=" + msg.MsgId + " MsgType" + msg.GetType().Name);
            return new TcpMessage(msg, data);
        }

        //used by FromArraySegment to set the values and return the struct
        private TcpMessage(Message message, ArraySegment<byte> data)
        {
            MessageType = message.GetType();
            Data = data;
            WrappedMessage = message;
        }

        /// <summary>
        /// Creates a tcp message for use in the tcp processor for sending messgaes over tcp
        /// </summary>
        /// <param name="message">the message to wrap</param>
        public TcpMessage(Message message)
        {
            Ensure.NotNull(message, nameof(message));
            MessageType = message.GetType();
            Log.Debug("Message MsgId=" + message.MsgId + " MsgTypeId=" + message.GetType().Name + " to be wrapped.");

            var typeName = MessageType.FullName;
            var json = JsonConvert.SerializeObject(message, Settings.JsonSettings);

            var typeNameByteCount = Encoding.GetByteCount(typeName);
            var jsonByteCount = Encoding.GetByteCount(json);
            var totalByteCount = sizeof(int) + typeNameByteCount + sizeof(int) + jsonByteCount;

            var array = new byte[totalByteCount];
            Data = new ArraySegment<byte>(array);

            var offset = 0;
            offset += WriteBytes(typeNameByteCount, array, offset);
            offset += WriteBytes(typeName, array, offset);
            offset += WriteBytes(jsonByteCount, array, offset);
            WriteBytes(json, array, offset);

            WrappedMessage = message;
        }

        private static int ReadBytes(byte[] source, int offset, out int destination)
        {
            destination = BitConverter.ToInt32(source, offset);
            return sizeof(int);
        }

        private static int WriteBytes(int source, byte[] destination, int offset)
        {
            destination[offset + 0] = (byte)source;
            destination[offset + 1] = (byte)(source >> 8);
            destination[offset + 2] = (byte)(source >> 16);
            destination[offset + 3] = (byte)(source >> 24);
            return sizeof(int);
        }

        private static int ReadBytes(byte[] source, int offset, int count, out string destination)
        {
            destination = Encoding.GetString(source, offset, count);
            return count;
        }

        private static int WriteBytes(string source, byte[] destination, int offset)
        {
            return Encoding.GetBytes(source, 0, source.Length, destination, offset);
        }
    }
}
