using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber
{
    public static class QueuedSubscriberLock
    {
        public static readonly object LockObject = new object();
    }
}
