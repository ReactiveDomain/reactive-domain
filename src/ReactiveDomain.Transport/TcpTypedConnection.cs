

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using ReactiveDomain.Logging;
using ReactiveDomain.Transport.Formatting;
using ReactiveDomain.Transport.Framing;

namespace ReactiveDomain.Transport
{
    public class TcpTypedConnection<T>
    {
        // ReSharper disable once StaticMemberInGenericType
        private static readonly ILogger Log = LogManager.GetLogger("ReactiveDomain");

        public event Action<TcpTypedConnection<T>, SocketError> ConnectionClosed;

        private readonly ITcpConnection _connection;
        private readonly IMessageFormatter<T> _formatter;
        private readonly IMessageFramer _framer;

        private Action<TcpTypedConnection<T>, T> _receiveCallback;

        public EndPoint RemoteEndPoint { get { return _connection.RemoteEndPoint; } }
        public EndPoint LocalEndPoint { get { return _connection.LocalEndPoint; } }

        public int SendQueueSize
        {
            get { return _connection.SendQueueSize; }
        }

        public TcpTypedConnection(ITcpConnection connection,
                                  IMessageFormatter<T> formatter,
                                  IMessageFramer framer)
        {
            _connection = connection;
            _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
            _framer = framer ?? throw new ArgumentNullException(nameof(framer));

            connection.ConnectionClosed += OnConnectionClosed;

            //Setup callback for incoming messages
            framer.RegisterMessageArrivedCallback(IncomingMessageArrived);
        }

        private void OnConnectionClosed(ITcpConnection connection, SocketError socketError)
        {
            connection.ConnectionClosed -= OnConnectionClosed;

            var handler = ConnectionClosed;
            if (handler != null)
                handler(this, socketError);
        }

        public void EnqueueSend(T message)
        {
            var data = _formatter.ToArraySegment(message);
            _connection.EnqueueSend(_framer.FrameData(data));
        }

        public void ReceiveAsync(Action<TcpTypedConnection<T>, T> callback)
        {
            if (_receiveCallback != null)
                throw new InvalidOperationException("ReceiveAsync should be called just once.");

            _receiveCallback = callback ?? throw new ArgumentNullException(nameof(callback));

            _connection.ReceiveAsync(OnRawDataReceived);
        }

        private void OnRawDataReceived(ITcpConnection connection, IEnumerable<ArraySegment<byte>> data)
        {
            try
            {
                _framer.UnFrameData(connection.ConnectionId, data);
            }
            catch (PackageFramingException exc)
            {
                Log.InfoException(exc,"Invalid TCP frame received.");
                Close("Invalid TCP frame received.");
                return;
            }
            connection.ReceiveAsync(OnRawDataReceived);
        }

        private void IncomingMessageArrived(Guid connectionId, ArraySegment<byte> message)
        {
            _receiveCallback(this, _formatter.From(message));
        }

        public void Close(string reason = null)
        {
            _connection.Close(reason);
        }
    }
}