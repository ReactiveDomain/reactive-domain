using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using ReactiveDomain.Messaging;
using ReactiveDomain.Logging;
using ReactiveDomain.Util;
using Settings = ReactiveDomain.Messaging.Json;

namespace ReactiveDomain.Transport.CommandSocket
{
    public struct TcpMessage
    {
        private static readonly ILogger Log = LogManager.GetLogger("ReactiveDomain");
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

            Message msg;
            using (var reader = new BsonDataReader(new MemoryStream(data.Array)))
            {
                reader.Read(); //object
                reader.Read(); //property name

                var messageType = MessageHierarchy.GetTypeByFullName((string)reader.Value);
                reader.Read(); //property value
                msg = (Message)JsonConvert.DeserializeObject((string)reader.Value, messageType, Settings.JsonSettings);
            }
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
    }

}
