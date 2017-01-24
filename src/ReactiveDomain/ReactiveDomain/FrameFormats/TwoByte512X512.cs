using System;
using System.Runtime.InteropServices;
using NLog;
using ReactiveDomain.Memory;
using ReactiveDomain.Util;

namespace ReactiveDomain.FrameFormats
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct TwoByte512X512Frame
    {
        public long FrameNumber;
        public fixed byte FrameId[16];
        public fixed byte VideoId[16];
        public double OffsetMilliseconds;
        public fixed byte pixels[2 * 512 * 512];
    }
    public unsafe class TwoByte512X512Image : Image
    {
        private static readonly Logger Log = LogManager.GetLogger("Common");

        public TwoByte512X512Image(byte* buffer)
            : base(buffer, sizeof(TwoByte512X512Frame))
        {
        }

        public TwoByte512X512Image()
        {
            BufferSize = sizeof(TwoByte512X512Frame);
        }

        public override byte* PixelBuffer
        {
            get
            {
                CheckLifetime();
                return ((TwoByte512X512Frame*)Buffer)->pixels;
            }
        }

        public static int PixelBufferLength => 2 * 512 * 512;

        public override long FrameNumber
        {
            get
            {
                CheckLifetime();
                return ((TwoByte512X512Frame*)Buffer)->FrameNumber;
            }
            set
            {
                CheckLifetime();
                Ensure.Positive(value, "FrameNumber");
                ((TwoByte512X512Frame*)Buffer)->FrameNumber = value;
            }
        }

        public override Guid VideoId
        {
            get
            {
                CheckLifetime();
                return Utility.ParseGuidBuffer(((TwoByte512X512Frame*)Buffer)->VideoId);
            }
            set
            {
                CheckLifetime();
                if (value == Guid.Empty)
                {
                    Log.Error("VideoId cannot be an empty Guid.");
                    throw new ArgumentException("VideoId cannot be an empty Guid.");
                }
                value.CopyToBuffer(((TwoByte512X512Frame*)Buffer)->VideoId);
            }
        }

        public override Guid FrameId
        {
            get
            {
                CheckLifetime();
                return Utility.ParseGuidBuffer(((TwoByte512X512Frame*)Buffer)->FrameId);
            }
            set
            {
                CheckLifetime();
                if (value == Guid.Empty)
                {
                    Log.Error("FrameId cannot be an empty Guid.");
                    throw new ArgumentException("FrameId cannot be an empty Guid.");
                }
                value.CopyToBuffer(((TwoByte512X512Frame*)Buffer)->FrameId);
            }
        }

        public override double Offset
        {
            get
            {
                CheckLifetime();
                return ((TwoByte512X512Frame*)Buffer)->OffsetMilliseconds;
            }
            set
            {
                CheckLifetime();
                if (value < 0)
                {
                    Log.Error("Offset must be a positive value.");
                    throw new ArgumentException("Offset must be a positive value.");
                }
                ((TwoByte512X512Frame*)Buffer)->OffsetMilliseconds = value;
            }
        }
    }
}
