using ReactiveDomain.Messaging;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation
{
    public class StreamStoreMsgs
    {
        public class CatchupSubscriptionBecameLive : Message
        {
            public CatchupSubscriptionBecameLive()
            {
            }
        }
    }
}
