using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace ReactiveDomain.Transport.Tests
{
    public class MockTcpConnection : ITcpConnection
    {
        private static IPEndPoint _remoteEndPoint;
        private static Guid _connectionId;

        public static MockTcpConnection CreateConnectingTcpConnection(Guid connectionId,
                                                                    IPEndPoint remoteEndPoint,
                                                                    TcpClientConnector connector,
                                                                    TimeSpan connectionTimeout,
                                                                    Action<ITcpConnection> onConnectionEstablished,
                                                                    Action<ITcpConnection, SocketError> onConnectionFailed,
                                                                    bool verbose)
        {
            _connectionId = connectionId;
            _remoteEndPoint = remoteEndPoint;
            return new MockTcpConnection();
        }

        public static MockTcpConnection CreateAcceptedTcpConnection(Guid connectionId, IPEndPoint remoteEndPoint, Socket socket, bool verbose)
        {
            throw new NotImplementedException();
        }

        public event Action<ITcpConnection, SocketError> ConnectionClosed;

        public Guid ConnectionId => _connectionId;

        public IPEndPoint RemoteEndPoint => _remoteEndPoint;

        public IPEndPoint LocalEndPoint => null;

        public int SendQueueSize
        {
            get { throw new NotImplementedException(); }
        }

        public bool IsInitialized => false;

        public bool IsClosed => false;

        public bool IsReadyForSend => true;
        public bool IsReadyForReceive => true;
        public bool IsFaulted => false;

        public void EnqueueSend(IEnumerable<ArraySegment<byte>> data)
        {
            SentData = data;
            if (_callback != null)
            {
                _callback(this, ResponseData);
                _callback = null;
            }

        }

        public IEnumerable<ArraySegment<byte>> SentData { get; set; }
        public IEnumerable<ArraySegment<byte>> ResponseData { get; set; }

        private Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> _callback;

        public void ReceiveAsync(Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> callback)
        {
            _callback = callback;
        }

        public void Close(string reason)
        {
            ConnectionClosed?.Invoke(this, SocketError.Success);
        }

        public override string ToString()
        {
            return RemoteEndPoint.ToString();
        }

    }
}