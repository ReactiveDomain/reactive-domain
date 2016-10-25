using System.Threading;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.EventStore
{
    public class EventStoreMsg
    {
        public class CatchupSubscriptionBecameLive : Message
        {
           
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;

            public CatchupSubscriptionBecameLive()
            {
            }
        }
    }
}
