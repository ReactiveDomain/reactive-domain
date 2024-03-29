

using System;
using System.Collections.Generic;

namespace ReactiveDomain.Transport.Framing
{
    public class StxEtxMessageFramer : IMessageFramer
    {
        private const int STX = 2;
        private const int ETX = 3;

        private static readonly ArraySegment<byte> STXBUFFER = new ArraySegment<byte>(new byte[] { STX });
        private static readonly ArraySegment<byte> ETXBUFFER = new ArraySegment<byte>(new byte[] { ETX });

        private enum ParserState
        {
            AwaitingEtx,
            AwaitingStx
        }

        private byte[] _messageBuffer;
        private ParserState _currentState = ParserState.AwaitingStx;
        private int _bufferIndex;
        private Action<Guid, ArraySegment<byte>> _receivedHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="StxEtxMessageFramer"/> class.
        /// </summary>
        /// <param name="initialBufferSize">Initial size of the Buffer.</param>
        public StxEtxMessageFramer(int initialBufferSize)
        {
            if (initialBufferSize < 1) throw new ArgumentException("Buffer size must be greater than zero.");
            _messageBuffer = new byte[initialBufferSize];
            _currentState = ParserState.AwaitingStx;
        }

        public void UnFrameData(Guid routeId, IEnumerable<ArraySegment<byte>> data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            foreach (ArraySegment<byte> buffer in data)
            {
                Parse(routeId, buffer);
            }
        }

        public void UnFrameData(Guid routeId, ArraySegment<byte> data)
        {
            Parse(routeId, data);
        }

        /// <summary>
        /// Parses a stream chunking based on STX/ETX framing. Calls are re-entrant and hold state internally.
        /// </summary>
        /// <param name="routeId">The ID of the source from which the data were routed.</param>
        /// <param name="bytes">A byte array of data to append</param>
        private void Parse(Guid routeId, ArraySegment<byte> bytes)
        {
            byte[] data = bytes.Array;
            for (int i = bytes.Offset; i < bytes.Offset + bytes.Count; i++)
            {
                if ((data[i] > 3 || data[i] == 1) && _currentState == ParserState.AwaitingEtx)
                {
                    if (_bufferIndex == _messageBuffer.Length)
                    {
                        var tmp = new byte[_messageBuffer.Length * 2];
                        Buffer.BlockCopy(_messageBuffer, 0, tmp, 0, _messageBuffer.Length);
                        _messageBuffer = tmp;
                    }
                    _messageBuffer[_bufferIndex] = data[i];
                    _bufferIndex++;
                }
                else if (data[i] == STX)
                {
                    _currentState = ParserState.AwaitingEtx;
                    _bufferIndex = 0;
                }
                else if (data[i] == ETX && _currentState == ParserState.AwaitingEtx)
                {
                    _currentState = ParserState.AwaitingStx;
                    if (_receivedHandler != null)
                        _receivedHandler(routeId, new ArraySegment<byte>(_messageBuffer, 0, _bufferIndex));
                    _bufferIndex = 0;
                }
            }
        }

        public IEnumerable<ArraySegment<byte>> FrameData(ArraySegment<byte> data)
        {
            yield return STXBUFFER;
            yield return data;
            yield return ETXBUFFER;
        }

        public void RegisterMessageArrivedCallback(Action<Guid, ArraySegment<byte>> handler)
        {
            _receivedHandler = handler ?? throw new ArgumentNullException(nameof(handler));
        }
    }
}