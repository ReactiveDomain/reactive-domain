namespace ReactiveDomain.Transport.Serialization
{
    using System;
    using ReactiveDomain.Messaging;

    public interface IMessageSerializer
    {
        IMessage FromBytes(ArraySegment<byte> data);
        ArraySegment<byte> ToBytes(IMessage message);
    }
}
