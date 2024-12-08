using System.Collections.Generic;
using ReactiveDomain.Messaging;
using ReactiveDomain.Testing;

namespace ReactiveDomain.Foundation.Tests.StreamListenerTests.Common
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
                },
                2000,
                $"Stream '{streamName}' not created");
        }
    }
}
