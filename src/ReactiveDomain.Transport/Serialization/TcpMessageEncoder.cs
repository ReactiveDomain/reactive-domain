using System;
using System.Text;
using ReactiveDomain.Messaging;
using ReactiveDomain.Util;

namespace ReactiveDomain.Transport.Serialization
{
    public class TcpMessageEncoder
    {
        private static readonly Encoding Encoding = Helper.UTF8NoBom;

        public Tuple<string, Type> FromBytes(ArraySegment<byte> data)
        {
            if (data.Array == null || data.Count < sizeof(int))
                throw new ArgumentException($"ArraySegment null or too short, length: {data.Count}", nameof(data));

            var offset = data.Offset;
            offset += ReadBytes(data.Array, offset, out var typeNameByteCount);
            offset += ReadBytes(data.Array, offset, typeNameByteCount, out var typeName);
            offset += ReadBytes(data.Array, offset, out var jsonByteCount);
            ReadBytes(data.Array, offset, jsonByteCount, out var json);

            var messageType = MessageHierarchy.GetTypeByFullName(typeName);
            return new Tuple<string, Type>(json, messageType);
        }

        public ArraySegment<byte> ToBytes(string jsonMessage, Type messageType)
        {
            Ensure.NotNullOrEmpty(jsonMessage, nameof(jsonMessage));
            Ensure.NotNull(messageType, nameof(messageType));

            var typeName = messageType.FullName;
            if (typeName is null)
                throw new Exception("Expected non-null type name.");

            var typeNameByteCount = Encoding.GetByteCount(typeName);
            var jsonByteCount = Encoding.GetByteCount(jsonMessage);
            var totalByteCount = sizeof(int) + typeNameByteCount + sizeof(int) + jsonByteCount;

            var array = new byte[totalByteCount];
            var data = new ArraySegment<byte>(array);

            var offset = 0;
            offset += WriteBytes(typeNameByteCount, array, offset);
            offset += WriteBytes(typeName, array, offset);
            offset += WriteBytes(jsonByteCount, array, offset);
            WriteBytes(jsonMessage, array, offset);

            return data;
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