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
        public VideoFrameHeader FrameHeader;
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
    }
}
