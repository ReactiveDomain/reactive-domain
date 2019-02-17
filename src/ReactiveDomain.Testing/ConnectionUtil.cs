using System.Threading;

namespace ReactiveDomain.Testing {
    public static class ConnectionUtil {
        public static bool TryConfirmStream(this IStreamStoreConnection conn, string streamTypeName, int expectedEventCount) {
            int count = 0;
            while (true) {
                try {
                    var slice = conn.ReadStreamForward(streamTypeName, StreamPosition.Start, 500);
                    if (slice.IsEndOfStream && slice.Events.Length == expectedEventCount) {
                        return true;
                    }
                    Thread.Sleep(10);
                    count++;
                    if (count > 100) return false;
                }
                catch {
                    //ignore
                }
            }
        }
    }
}
