using System.Collections.Generic;
using ReactiveDomain.Messaging;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Testing
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
