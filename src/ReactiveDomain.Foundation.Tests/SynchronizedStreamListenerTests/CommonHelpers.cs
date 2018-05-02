using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests.SynchronizedStreamListenerTests
{
    internal static class CommonHelpers
    {
        internal static void WaitForStream(IStreamStoreConnection conn, string streamName)
        {
            //wait for the categorty projection to be written
            Assert.IsOrBecomesTrue(
                () =>
                {
                    try
                    {
                        return conn.ReadStreamForward(streamName, 0, 1) != null;
                    }
                    catch
                    {
                        return false;
                    }
                });
        }

        internal static EventData[] GenerateEvents(IEventSerializer eventSerializer)
        {
            var eventsToSave = new[]
            {
                eventSerializer.Serialize(
                    new TestEvent(CorrelatedMessage.NewRoot()),
                    new Dictionary<string, object>())
            };
            return eventsToSave;
        }
    }
}
