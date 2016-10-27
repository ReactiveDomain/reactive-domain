using System;
using System.Runtime.InteropServices;
using System.Threading;
using NLog;
using ReactiveDomain.Memory;
using ReactiveDomain.Util;



namespace ReactiveDomain.FrameFormats
{
    //n.b. The layout of the first 4 fields for VideoFrameHeader, RawMonoFrame and RawColorFrame must match
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct VideoFrameHeader
    {
        public long FrameNumber;
        public fixed byte FrameId[16];
        public fixed byte VideoId[16];
        public double OffsetMilliseconds;
    }
    
    /// <summary>
    /// n.b. the Frame does not own the memory in the buffer
    /// the byte* passed in must be pinned and managed outside 
    /// the frame. Generally this memory will be owned by an 
    /// instance of the RawFrameBuffer or VideoWriter class.
    /// </summary>
    public abstract unsafe class Image : IDisposable
    {
        private static readonly Logger Log = NLog.LogManager.GetLogger("Common");
        private bool _disposed;
        private byte* _buffer;
        public byte* Buffer
        {
            get { CheckLifetime(); return _buffer; }
            private set { _buffer = value; }
        }
        public int BufferSize { get; protected set; }


        private const long True = 1;
        private const long False = 0;
        private long _bufferAdded = False;
        protected Image(byte* buffer, int bufferSize)
        {
            Ensure.Positive((long)buffer, "buffer");//Null ptr test
            Ensure.Positive(bufferSize, "bufferSize");
            _buffer = buffer;
            BufferSize = bufferSize;
            Interlocked.Exchange(ref _bufferAdded, True);
        }
        //n.b this should only be be called by generic or refection constructors
        protected Image()
        {
        }
        //n.b. this should only be called once and only when using the parameterless ctor
        public void SetBuffer(byte* buffer, int bufferSize)
        {

            if (Interlocked.CompareExchange(ref _bufferAdded, True, False) != False)
            {
                Log.Error("Buffer can only be set once!");
                throw new InvalidOperationException("Buffer can only be set once!");
            }

            Ensure.Equal(BufferSize, bufferSize, "bufferSize");
            Ensure.Positive((long)buffer, "buffer");//Null ptr test
            _buffer = buffer;
           
        }

       
        public abstract byte* PixelBuffer { get; }
        public abstract Guid VideoId { get; set; }
        public abstract Guid FrameId { get; set; }
        public abstract long FrameNumber { get; set; }
        public abstract double Offset { get; set; }

        public VideoFrameHeader Header => *(VideoFrameHeader*) _buffer;

        public void CopyFrom(Image other)
        {
            CheckLifetime();
            Ensure.Equal(BufferSize, other.BufferSize, "other");
            Utility.CopyMemory(Buffer, other.Buffer, (uint)BufferSize);
        }
        public void Clear()
        {
            CheckLifetime();
            Utility.Clear((IntPtr)Buffer, sizeof(ThreeByte1024X1024Frame));
        }
        #region IDisposable
        public void Dispose()
        {
            DisposeInternal();
            GC.SuppressFinalize(this);
        }

        ~Image()
        {
            DisposeInternal();
        }

        private void DisposeInternal()
        {
            if (_disposed) return;
            Buffer = (byte*)IntPtr.Zero;
            _disposed = true;
        }

        protected void CheckLifetime()
        {
            if (Interlocked.Read(ref _bufferAdded) != True)
            {
                Log.Error("No buffer has been added!");
                throw new InvalidOperationException("No buffer has been added!");
            }
            if (_disposed)
            {
                Log.Error("Object has been disposed.");
                throw new ObjectDisposedException("Object has been disposed.");
            }
        }
        #endregion
    }
}
