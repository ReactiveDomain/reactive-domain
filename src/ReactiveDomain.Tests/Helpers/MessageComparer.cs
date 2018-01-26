using System.Collections.Generic;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Tests.Helpers
{
    public class MessageIdComparer: IEqualityComparer<Message>
    {
        public bool Equals(Message x, Message y)
        {
            return x.MsgId == y.MsgId;
        }

        public int GetHashCode(Message obj)
        {
            return obj.MsgId.GetHashCode();
        }
    }
}
