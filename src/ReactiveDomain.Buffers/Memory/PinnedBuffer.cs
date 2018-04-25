using System;
using System.Runtime.InteropServices;
using ReactiveDomain.Logging;

namespace ReactiveDomain.Buffers.Memory
{
    /// <summary>
    /// Image is one example of using a pinned buffer.
    /// The Wrapped buffer can also used to setup ring buffers for "disrupter" style high speed processing
    /// </summary>
    public sealed unsafe class PinnedBuffer : IDisposable
    {
        private static readonly ILogger Log = LogManager.GetLogger("Common");

        private readonly long _size;
        private GCHandle _bufferHandle;
        private readonly byte* _bufferPtr;
        private bool _disposed;

        public PinnedBuffer(long size)
        {
            var buffer = new byte[size];
            _bufferHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            _bufferPtr = (byte*)(_bufferHandle.AddrOfPinnedObject());
            _size = size;
            _available = true;
        }

        private bool _available;
        public bool Available
        {
            get
            {
                CheckDisposed();  
                return ! _disposed && _available;
            }
        }

        public void Reserve()
        {
            CheckDisposed();
            _available = false;
        }
        public void Release()
        {
            if (_disposed) return;
            Utility.Clear((IntPtr)_bufferPtr,(int)_size);
            _available = true;
        }

        public byte* Buffer
        {
            get
            {
                CheckDisposed();
                return _bufferPtr;
            }
        }

        public long Length
        {
            get
            {
                CheckDisposed(); 
                return _size;
            }
        }

        #region IDisposable
        /// <summary>
        /// Returns any memory used by this <see cref="PinnedBuffer"></see>
        /// </summary>
        public void Dispose()
        {
            DisposeInternal();
            GC.SuppressFinalize(this);
        }

        ~PinnedBuffer()
        {
            DisposeInternal();
        }

        private void DisposeInternal()
        {
            if (_disposed) return;
            Log.Debug("Disposing buffer of size " + _size + " - it should not be used anymore.");
            _bufferHandle.Free();
            _disposed = true;
        }


        private void CheckDisposed()
        {
            if (_disposed)
            {
                Log.Error("Buffer of size " + _size + " has been disposed and should not be used at this time.");
                throw new ObjectDisposedException("Buffer has been disposed.");
            }
        }
        #endregion


    }
}
