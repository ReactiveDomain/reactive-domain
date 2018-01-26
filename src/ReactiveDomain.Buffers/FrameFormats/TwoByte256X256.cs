using System.Runtime.InteropServices;
using ReactiveDomain.Core.Logging;

namespace ReactiveDomain.Buffers.FrameFormats
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct TwoByte256X256Frame
    {
        public VideoFrameHeader FrameHeader;
        public fixed byte pixels[2 * 256 * 256];
    }

    public unsafe class TwoByte256X256Image : Image
    {
        private static readonly ILogger Log = LogManager.GetLogger("Common");

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
