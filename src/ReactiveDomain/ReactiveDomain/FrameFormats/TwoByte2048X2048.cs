using System;
using System.Runtime.InteropServices;
using NLog;
using ReactiveDomain.Memory;
using ReactiveDomain.Util;

namespace ReactiveDomain.FrameFormats
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct TwoByte2048X2048Frame
    {
        public long FrameNumber;
        public fixed byte FrameId[16];
        public fixed byte VideoId[16];
        public double OffsetMilliseconds;
        public fixed byte pixels[2 * 2048 * 2048];
    }
    public unsafe class TwoByte2048X2048Image : Image
    {
        private static readonly Logger Log = LogManager.GetLogger("Common");

        public TwoByte2048X2048Image(byte* buffer)
            : base(buffer, sizeof(TwoByte2048X2048Frame))
        {
        }

        public TwoByte2048X2048Image()
        {
            BufferSize = sizeof(TwoByte2048X2048Frame);
        }

        public override byte* PixelBuffer
        {
            get
            {
                CheckLifetime();
                return ((TwoByte2048X2048Frame*)Buffer)->pixels;
            }
        }

        public static int PixelBufferLength => 2 * 2048 * 2048;

        public override long FrameNumber
        {
            get
            {
                CheckLifetime();
                return ((TwoByte2048X2048Frame*)Buffer)->FrameNumber;
            }
            set
            {
                CheckLifetime();
                Ensure.Positive(value, "FrameNumber");
                ((TwoByte2048X2048Frame*)Buffer)->FrameNumber = value;
            }
        }

        public override Guid VideoId
        {
            get
            {
                CheckLifetime();
                return Utility.ParseGuidBuffer(((TwoByte2048X2048Frame*)Buffer)->VideoId);
            }
            set
            {
                CheckLifetime();
                if (value == Guid.Empty)
                {
                    Log.Error("VideoId cannot be an empty Guid.");
                    throw new ArgumentException("VideoId cannot be an empty Guid.");
                }
                value.CopyToBuffer(((TwoByte2048X2048Frame*)Buffer)->VideoId);
            }
        }

        public override Guid FrameId
        {
            get
            {
                CheckLifetime();
                return Utility.ParseGuidBuffer(((TwoByte2048X2048Frame*)Buffer)->FrameId);
            }
            set
            {
                CheckLifetime();
                if (value == Guid.Empty)
                {
                    Log.Error("FrameId cannot be an empty Guid.");
                    throw new ArgumentException("FrameId cannot be an empty Guid.");
                }
                value.CopyToBuffer(((TwoByte2048X2048Frame*)Buffer)->FrameId);
            }
        }

        public override double Offset
        {
            get
            {
                CheckLifetime();
                return ((TwoByte2048X2048Frame*)Buffer)->OffsetMilliseconds;
            }
            set
            {
                CheckLifetime();
                if (value < 0)
                {
                    Log.Error("Offset must be a positive value.");
                    throw new ArgumentException("Offset must be a positive value.");
                }
                ((TwoByte2048X2048Frame*)Buffer)->OffsetMilliseconds = value;
            }
        }
    }
}
