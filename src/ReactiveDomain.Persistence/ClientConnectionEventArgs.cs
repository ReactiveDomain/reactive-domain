using System;
using System.Net;

namespace ReactiveDomain {
    public class ClientConnectionEventArgs : EventArgs
    {
        /// <summary>
        /// The endpoint of the Event Store server to or from which the connection was connected or disconnected.
        /// </summary>
        public IPEndPoint RemoteEndPoint { get; private set; }

        /// <summary>
        /// The <see cref="T:EventStore.ClientAPI.IEventStoreConnection" /> responsible for raising the event.
        /// </summary>
        public IStreamStoreConnection Connection { get; private set; }

        /// <summary>
        /// Constructs a new instance of <see cref="T:EventStore.ClientAPI.ClientConnectionEventArgs" />.
        /// </summary>
        /// <param name="connection">The <see cref="T:EventStore.ClientAPI.IEventStoreConnection" /> responsible for raising the event.</param>
        /// <param name="remoteEndPoint">The endpoint of the Event Store server to or from which the connection was connected or disconnected.</param>
        public ClientConnectionEventArgs(IStreamStoreConnection connection, IPEndPoint remoteEndPoint)
        {
            this.Connection = connection;
            this.RemoteEndPoint = remoteEndPoint;
        }
    }
}