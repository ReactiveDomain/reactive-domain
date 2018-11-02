namespace ReactiveDomain.Transport.Serialization
{
    using System;
    using ReactiveDomain.Messaging;

    public interface IMessageSerializer {
        Message FromBytes(ArraySegment<byte> data);
        ArraySegment<byte> ToBytes(Message message);
    }
}
