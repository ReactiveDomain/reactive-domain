using System.Collections.Generic;
using ReactiveDomain.Messaging;
using ReactiveDomain.Testing;

namespace ReactiveDomain.Foundation.Tests.SynchronizedStreamListenerTests
{
    internal static class CommonHelpers
    {
        internal static void WaitForStream(IStreamStoreConnection conn, string streamName)
        {
            //wait for the category projection to be written
            AssertEx.IsOrBecomesTrue(
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
