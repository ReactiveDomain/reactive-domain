using System;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Transport.Serialization
{
    public interface IMessageSerializer
    {
        IMessage DeserializeMessage(string json, Type messageType);
        string SerializeMessage(IMessage message);
    }
}
