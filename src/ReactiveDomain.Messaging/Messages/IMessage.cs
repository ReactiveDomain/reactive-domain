using System;

namespace ReactiveDomain.Messaging
{
    public interface IMessage
    {
        Guid MsgId { get; }
    }
}
