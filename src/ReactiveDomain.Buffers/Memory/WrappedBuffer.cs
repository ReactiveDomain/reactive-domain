using System;
using ReactiveDomain.Messaging.Logging;

namespace ReactiveDomain.Buffers.Memory
{
    public unsafe class WrappedBuffer : IDisposable
    {
        private static readonly ILogger Log = LogManager.GetLogger("Common");
        private readonly BufferManager _manager;
        private readonly PinnedBuffer _buffer;
        private bool _disposed;

        public WrappedBuffer(BufferManager manager, PinnedBuffer buffer)
        {
            _manager = manager;
            _buffer = buffer;
        }

        public byte* Buffer
        {
            get
            {
                CheckDisposed();
                return _buffer.Buffer;
            }
        }

        public long Length {
            get
            {
                CheckDisposed();
                return _buffer.Length;
            }
        }

        public void Close()
        {
            Dispose();
        }

        private void CheckDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException("BufferWrapper has been disposed.");

        }

        public void Dispose()
        {
            if (_disposed) return;

            _manager.Return(_buffer);
            _disposed = true;

        }
    }
}
