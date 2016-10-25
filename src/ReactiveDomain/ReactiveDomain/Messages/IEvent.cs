using System;

namespace ReactiveDomain.Messages
{
    public interface IEvent
    {
        Guid MsgId { get; }
    }
}
