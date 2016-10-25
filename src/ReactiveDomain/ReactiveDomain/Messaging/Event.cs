using System.Threading;
using ReactiveDomain.Messages;

namespace ReactiveDomain.Messaging
{
    public class Event : Message, IEvent
    {
        protected ushort Version = 1;
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId { get { return TypeId; } }
        public Event()
        {

        }
    }
}
