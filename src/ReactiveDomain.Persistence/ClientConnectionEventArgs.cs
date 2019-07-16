using System;
using System.Net;

namespace ReactiveDomain {
    /// <summary>
    /// Event Arguments for the event raised when an <see cref="T:ReactiveDomain.IEventStoreConnection" /> is
    /// connected to or disconnected from an Event Store server.
    /// </summary>
    public class ClientConnectionEventArgs : EventArgs
    {
        /// <summary>
        /// The endpoint of the Event Store server to or from which the connection was connected or disconnected.
        /// </summary>
        public IPEndPoint RemoteEndPoint { get; }

        /// <summary>
        /// The <see cref="T:ReactiveDomain.IEventStoreConnection" /> responsible for raising the event.
        /// </summary>
        public IStreamStoreConnection Connection { get; }

        /// <summary>
        /// Constructs a new instance of <see cref="T:ReactiveDomain.ClientConnectionEventArgs" />.
        /// </summary>
        /// <param name="connection">The <see cref="T:ReactiveDomain.IEventStoreConnection" /> responsible for raising the event.</param>
        /// <param name="remoteEndPoint">The endpoint of the Event Store server to or from which the connection was connected or disconnected.</param>
        public ClientConnectionEventArgs(IStreamStoreConnection connection, IPEndPoint remoteEndPoint) {
            Connection = connection;
            RemoteEndPoint = remoteEndPoint;
        }
    }
}