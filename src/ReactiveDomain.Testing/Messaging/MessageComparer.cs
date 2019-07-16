using System.Collections.Generic;
using ReactiveDomain.Messaging;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Testing
{
    public class MessageIdComparer: IEqualityComparer<IMessage>
    {
        public bool Equals(IMessage x, IMessage y)
        {
            return x.MsgId == y.MsgId;
        }

        public int GetHashCode(IMessage obj)
        {
            return obj.MsgId.GetHashCode();
        }
    }
}
