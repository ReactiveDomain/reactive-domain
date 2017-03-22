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
        public VideoFrameHeader FrameHeader;
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

        public static int PixelBufferLength => 2 * 256 * 256;
    }
}
