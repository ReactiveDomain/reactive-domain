using System;
using System.Runtime.InteropServices;
using NLog;
using ReactiveDomain.Memory;
using ReactiveDomain.Util;

namespace ReactiveDomain.FrameFormats
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct TwoByte256X256Frame
    {
        public long FrameNumber;
        public fixed byte FrameId[16];
        public fixed byte VideoId[16];
        public double OffsetMilliseconds;
        public fixed byte pixels[2 * 256 * 256];
    }
    public unsafe class TwoByte256X256Image : Image
    {
        private static readonly Logger Log = LogManager.GetLogger("Common");

        public TwoByte256X256Image(byte* buffer)
            : base(buffer, sizeof(TwoByte256X256Frame))
        {
        }

        public TwoByte256X256Image()
        {
            BufferSize = sizeof(TwoByte256X256Frame);
        }

        public override byte* PixelBuffer
        {
            get
            {
                CheckLifetime();
                return ((TwoByte256X256Frame*)Buffer)->pixels;
            }
        }

        public static int PixelBufferLength => 2 * 256 * 256;

        public override long FrameNumber
        {
            get
            {
                CheckLifetime();
                return ((TwoByte256X256Frame*)Buffer)->FrameNumber;
            }
            set
            {
                CheckLifetime();
                Ensure.Positive(value, "FrameNumber");
                ((TwoByte256X256Frame*)Buffer)->FrameNumber = value;
            }
        }

        public override Guid VideoId
        {
            get
            {
                CheckLifetime();
                return Utility.ParseGuidBuffer(((TwoByte256X256Frame*)Buffer)->VideoId);
            }
            set
            {
                CheckLifetime();
                if (value == Guid.Empty)
                {
                    Log.Error("VideoId cannot be an empty Guid.");
                    throw new ArgumentException("VideoId cannot be an empty Guid.");
                }
                value.CopyToBuffer(((TwoByte256X256Frame*)Buffer)->VideoId);
            }
        }

        public override Guid FrameId
        {
            get
            {
                CheckLifetime();
                return Utility.ParseGuidBuffer(((TwoByte256X256Frame*)Buffer)->FrameId);
            }
            set
            {
                CheckLifetime();
                if (value == Guid.Empty)
                {
                    Log.Error("FrameId cannot be an empty Guid.");
                    throw new ArgumentException("FrameId cannot be an empty Guid.");
                }
                value.CopyToBuffer(((TwoByte256X256Frame*)Buffer)->FrameId);
            }
        }

        public override double Offset
        {
            get
            {
                CheckLifetime();
                return ((TwoByte256X256Frame*)Buffer)->OffsetMilliseconds;
            }
            set
            {
                CheckLifetime();
                if (value < 0)
                {
                    Log.Error("Offset must be a positive value.");
                    throw new ArgumentException("Offset must be a positive value.");
                }
                ((TwoByte256X256Frame*)Buffer)->OffsetMilliseconds = value;
            }
        }
    }
}
