using System;
using System.Runtime.InteropServices;
using System.Threading;
using ReactiveDomain.Buffers.Memory;
using ReactiveDomain.Logging;
using ReactiveDomain.Util;

namespace ReactiveDomain.Buffers.Examples
{
    /// <summary>
    /// FrameBufferHeader structure represents the metadata structure 
    /// at the beginning of the RawFrameBuffer  
    /// </summary>
    //N.B. developers: all fields must be blittable types!!!
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct VideoSliceHeader
    {
        public uint Version;
        public uint FrameSize;
        public uint FrameCount;
        public fixed byte BufferId[16];
        public fixed byte pad[4]; //padding to allow pixel buffer to start on 64 byte boundary
    }

    public unsafe class ColorVideoSlice : VideoSlice
    {
        public ColorVideoSlice(uint frameCount, Func<long, WrappedBuffer> getBuffer)
            :base(frameCount, (uint)sizeof(ThreeByte1024X1024Frame), getBuffer)
        {
            
        }
        /// <summary>
        /// Returns a pointer to the frame at the specified index
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public new ThreeByte1024X1024Image this[uint index]
        {
            get
            {
                base.CheckDisposed();
                Ensure.LessThan(Count, index, "index");
                return new ThreeByte1024X1024Image(Frames[index]);
            }
        }
    }
    public unsafe class MonoVideoSlice : VideoSlice
    {
        public MonoVideoSlice(uint frameCount, Func<long, WrappedBuffer> getBuffer)
            : base(frameCount, (uint)sizeof(TwoByte1024X1024Frame), getBuffer)
        {

        }
        /// <summary>
        /// Returns a pointer to the frame at the specified index
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public new TwoByte1024X1024Image this[uint index]
        {
            get
            {
                base.CheckDisposed();
                Ensure.LessThan(Count, index, "index");
                return new TwoByte1024X1024Image(Frames[index]);
            }
        }
    }
    public unsafe class VideoSlice : IDisposable
    {
        private static readonly ILogger Log = LogManager.GetLogger("Common");
        private const uint Version = 1;
        private readonly WrappedBuffer _buffer;
        private readonly VideoSliceHeader* _header;
        protected readonly byte*[] Frames;

        private bool _disposed;

        public VideoSlice(
                    uint frameCount,
                    uint frameSize,
                    Func<long, WrappedBuffer> getBuffer)
        {
            var bufferId = Guid.NewGuid();
            var bufferSize = sizeof(VideoSliceHeader) + ((frameCount + 1) * frameSize);
            _buffer = getBuffer(bufferSize);
            _header = (VideoSliceHeader*)_buffer.Buffer;
            _header->Version = Version;
            _header->FrameCount = frameCount;
            _header->FrameSize = frameSize;
            bufferId.CopyToBuffer(_header->BufferId);
            Frames = new byte*[frameCount];
            _frameData = _buffer.Buffer + sizeof(VideoSliceHeader);
            for (int i = 0; i < frameCount; i++)
            {
                Frames[i] = _frameData + (i * frameSize);
            }
        }

        private const long Locked = 1;
        private const long Unlocked = 0;
        private long _flushLock = Unlocked;

        public bool TryLock()
        {
            //n.b. never set _flushlock to null or this will throw.
            Interlocked.CompareExchange(ref _flushLock, Locked, Unlocked);
            return IsLocked();
        }

        public void Unlock()
        {
            Interlocked.Exchange(ref _flushLock, Unlocked);
        }

        public bool IsLocked()
        {
            return Interlocked.Read(ref _flushLock) == Locked;
        }

        /// <summary>
        /// Pointer to the beginning of the buffer including the header
        /// </summary>
        public byte* AsBytePtr
        {
            get
            {
                CheckDisposed();
                return _buffer.Buffer;
            }
        }
        private readonly byte* _frameData;
        public byte* FramesBuffer
        {
            get
            {
                CheckDisposed();
                return _frameData;
            }
        }

        public uint FramesBufferSize
        {
            get
            {
                CheckDisposed();
                return FrameSize * Count;
            }
        }

        /// <summary>
        /// The size of the allocated buffer in bytes
        /// </summary>
        public long BufferSize
        {
            get
            {
                CheckDisposed();
                var size =
                    (FrameSize * Count)
                    + sizeof(VideoSliceHeader);
                return size;
            }
        }

        public Guid Id
        {
            get
            {
                CheckDisposed();
                return Utility.ParseGuidBuffer(_header->BufferId);
            }
        }

        public VideoSliceHeader Header
        {
            get
            {
                CheckDisposed();
                return *_header;
            }
        }
        /// <summary>
        /// Bytes per Frame
        /// </summary>
        public uint FrameSize
        {
            get
            {
                CheckDisposed();
                return _header->FrameSize;
            }
        }
        /// <summary>
        /// The number of allocated frames 
        /// </summary>
        public uint Count
        {
            get
            {
                CheckDisposed();
                return _header->FrameCount;
            }
        }
        /// <summary>
        /// Returns a pointer to the frame at the specified index
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public byte* this[uint index]
        {
            get
            {
                CheckDisposed();
                Ensure.LessThan(Count, index, "index");
                return Frames[index];
            }
        }
        /// <summary>
        /// Clears buffer and assigns a new BufferId
        /// </summary>
        public bool TryReset()
        {

            if (_disposed || IsLocked())
                return false;

            //Clear Frame Data
            Utility.Clear((IntPtr)Frames[0], (int)(_header->FrameCount * _header->FrameSize));
            //insert new buffer id
            var newId = Guid.NewGuid();
            newId.CopyToBuffer(_header->BufferId);
            return true;
        }
        /// <summary>
        /// Copies the content of the source into the buffer
        /// overwriting all data
        /// The Source must be of matching size and version
        /// </summary>
        /// <param name="source"></param>
        public void CopyFrom(byte* source)
        {
            var header = (VideoSliceHeader*)source;
            if (header->Version != Version)
            {
                Log.Error("CopyFrom() error: source version (" + header->Version + ") not equal my version (" + Version + ")");
                throw new ArgumentOutOfRangeException("source", "Incompatible Buffer Version");
            }
            if (header->FrameCount != _header->FrameCount)
            {
                Log.Error("CopyFrom() error: source framecount (" + header->FrameCount + ") not equal my framecount (" + _header->FrameCount + ")");
                throw new ArgumentOutOfRangeException("source", "Non-matching frame count.");
            }
            if (header->FrameSize != _header->FrameSize)
            {
                Log.Error("CopyFrom() error: source frame size (" + header->FrameSize + ") not equal my frame size (" + _header->FrameSize + ")");
                throw new ArgumentOutOfRangeException("source", "Non-matching frame size.");
            }
            if (Utility.ParseGuidBuffer(header->BufferId) == Guid.Empty)
            {
                Log.Error("CopyFrom() error: source BufferId is missing/invalid");
                throw new ArgumentOutOfRangeException("source", "Missing BufferId.");
            }
            Utility.CopyMemory((IntPtr)_buffer.Buffer, (IntPtr)source, (uint)BufferSize);
        }

        public void CopyFrom(VideoSlice other)
        {
            CopyFrom(other._buffer.Buffer);
        }

        #region IDisposable
        /// <summary>
        /// Returns any memory used by this <see cref="FramesBuffer"></see>
        /// </summary>
        public virtual void Dispose()
        {
            Disposepublic();
            GC.SuppressFinalize(this);
        }

        ~VideoSlice()
        {
            Disposepublic();
        }

        protected void Disposepublic()
        {
            if (_disposed) return;
            _buffer?.Dispose();
            _disposed = true;
        }


        protected void CheckDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException("Buffer has been disposed.");
        }
        #endregion
    }
}
