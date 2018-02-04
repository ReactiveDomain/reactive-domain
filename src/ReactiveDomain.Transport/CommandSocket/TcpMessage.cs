using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Logging;
using ReactiveDomain.Messaging.Util;
using Settings = ReactiveDomain.Messaging.Util.Json;

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

        /// <summary>
        /// Builds a tcp messages from a wrapped data array
        /// used by the tcp processing system to deserialize recieved data
        /// </summary>
        /// <param name="data">The wrapped memory array, the first 4 bytes should hold the length of the remaining data in bytes.</param>
        /// <returns>a new tcp message built from the array segment</returns>
        /// <exception cref="UnregisteredMessageException"> If the message type is not found this will throw UnregisteredMessageException</exception>
        public static TcpMessage FromFramedArraySegment(ArraySegment<byte> data)
        {
            if (data.Array == null || data.Count < sizeof(int))
                throw new ArgumentException($"ArraySegment null or too short, length: {data.Count}", nameof(data));
            
            var ms = new MemoryStream(data.Array, data.Offset +4 , data.Count - 4);
            Message msg;

            using (var reader = new BsonDataReader(ms))
            {
                reader.Read(); //object
                reader.Read(); //property name

                var messageType = MessageHierarchy.GetMsgType((string)reader.Value);
                reader.Read(); //property value
                msg = (Message)JsonConvert.DeserializeObject((string)reader.Value, messageType, Settings.JsonSettings);
            }
            Log.Debug("Deserialized Message MsgId=" + msg.MsgId + " MsgTypeId=" + msg.MsgTypeId);
            return new TcpMessage(msg, data);
        }
        //used by FromFramedArraySegment to set the values and return the struct
        private TcpMessage(Message message, ArraySegment<byte> data)
        {
            MessageId = message.MsgTypeId;
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
            MessageId = message.MsgTypeId;
            MessageType = message.GetType();
            Log.Debug("Message MsgId=" + message.MsgId + " MsgTypeId=" + message.MsgTypeId + " to be wrapped.");

            var ms = new MemoryStream();
            using (var writer = new BsonDataWriter(ms))
            {
                writer.WriteStartObject();
                writer.WritePropertyName(MessageType.FullName);
                writer.WriteValue(JsonConvert.SerializeObject(message, Settings.JsonSettings));
                writer.WriteEnd();
            }
            Data = new ArraySegment<byte>(ms.ToArray());
            WrappedMessage = message;
        }
        /// <summary>
        /// Gets the raw data wrapped for tcp send by prefixing the data segment length into the first 4 bytes
        /// </summary>
        /// <returns></returns>
        public ArraySegment<byte> AsFramedArraySegment()
        {
            var res = new byte[sizeof(int) + Data.Count];
            res[MsgIdOffset] = (byte)MessageId;
            res[MsgIdOffset + 1] = (byte)(MessageId >> 8);
            res[MsgIdOffset + 2] = (byte)(MessageId >> 16);
            res[MsgIdOffset + 3] = (byte)(MessageId >> 24);
            // ReSharper disable once AssignNullToNotNullAttribute - not actually possible
            Buffer.BlockCopy(Data.Array, Data.Offset, res, res.Length - Data.Count, Data.Count);
            return new ArraySegment<byte>(res);
        }
    }

}
