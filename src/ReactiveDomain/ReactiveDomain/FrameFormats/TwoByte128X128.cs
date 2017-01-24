using System;
using System.Runtime.InteropServices;
using NLog;
using ReactiveDomain.Memory;
using ReactiveDomain.Util;

namespace ReactiveDomain.FrameFormats
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct TwoByte128X128Frame
    {
        public long FrameNumber;
        public fixed byte FrameId[16];
        public fixed byte VideoId[16];
        public double OffsetMilliseconds;
        public fixed byte pixels[2 * 128 * 128];
    }
    public unsafe class TwoByte128X128Image : Image
    {
        private static readonly Logger Log = LogManager.GetLogger("Common");

        public TwoByte128X128Image(byte* buffer)
            : base(buffer, sizeof(TwoByte128X128Frame))
        {
        }

        public TwoByte128X128Image()
        {
            BufferSize = sizeof(TwoByte128X128Frame);
        }

        public override byte* PixelBuffer
        {
            get
            {
                CheckLifetime();
                return ((TwoByte128X128Frame*)Buffer)->pixels;
            }
        }

        public static int PixelBufferLength => 2 * 128 * 128;

        public override long FrameNumber
        {
            get
            {
                CheckLifetime();
                return ((TwoByte128X128Frame*)Buffer)->FrameNumber;
            }
            set
            {
                CheckLifetime();
                Ensure.Positive(value, "FrameNumber");
                ((TwoByte128X128Frame*)Buffer)->FrameNumber = value;
            }
        }

        public override Guid VideoId
        {
            get
            {
                CheckLifetime();
                return Utility.ParseGuidBuffer(((TwoByte128X128Frame*)Buffer)->VideoId);
            }
            set
            {
                CheckLifetime();
                if (value == Guid.Empty)
                {
                    Log.Error("VideoId cannot be an empty Guid.");
                    throw new ArgumentException("VideoId cannot be an empty Guid.");
                }
                value.CopyToBuffer(((TwoByte128X128Frame*)Buffer)->VideoId);
            }
        }

        public override Guid FrameId
        {
            get
            {
                CheckLifetime();
                return Utility.ParseGuidBuffer(((TwoByte128X128Frame*)Buffer)->FrameId);
            }
            set
            {
                CheckLifetime();
                if (value == Guid.Empty)
                {
                    Log.Error("FrameId cannot be an empty Guid.");
                    throw new ArgumentException("FrameId cannot be an empty Guid.");
                }
                value.CopyToBuffer(((TwoByte128X128Frame*)Buffer)->FrameId);
            }
        }

        public override double Offset
        {
            get
            {
                CheckLifetime();
                return ((TwoByte128X128Frame*)Buffer)->OffsetMilliseconds;
            }
            set
            {
                CheckLifetime();
                if (value < 0)
                {
                    Log.Error("Offset must be a positive value.");
                    throw new ArgumentException("Offset must be a positive value.");
                }
                ((TwoByte128X128Frame*)Buffer)->OffsetMilliseconds = value;
            }
        }
    }
}
