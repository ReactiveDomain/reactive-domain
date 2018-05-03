using ReactiveDomain.Messaging;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation
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
