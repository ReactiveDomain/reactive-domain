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
        public VideoFrameHeader FrameHeader;
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

        public static int PixelBufferLength => 2 * 2048 * 2048;

    }
}
