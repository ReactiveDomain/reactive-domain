using System.Collections.Generic;

namespace ReactiveDomain.Messaging.Testing
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
