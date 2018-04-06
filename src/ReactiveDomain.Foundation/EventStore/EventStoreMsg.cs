using System.Threading;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Foundation.EventStore
{
    public class EventStoreMsg
    {
        public class CatchupSubscriptionBecameLive : Message
        {
            public CatchupSubscriptionBecameLive()
            {
            }
        }
    }
}
