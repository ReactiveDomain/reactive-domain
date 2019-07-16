using ReactiveDomain.Messaging;
using System;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation
{
    public class StreamStoreMsgs
    {
        public class CatchupSubscriptionBecameLive : IMessage
        {
            public Guid MsgId { get; private set; }
            public CatchupSubscriptionBecameLive()
            {
                MsgId = Guid.NewGuid();
            }
        }
    }
}
