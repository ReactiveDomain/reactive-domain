using System.Runtime.InteropServices;
using ReactiveDomain.Messaging.Logging;

namespace ReactiveDomain.Buffers.FrameFormats
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct TwoByte2048X2048Frame
    {
        public VideoFrameHeader FrameHeader;
        public fixed byte pixels[2 * 2048 * 2048];
    }
    public unsafe class TwoByte2048X2048Image : Image
    {
        private static readonly ILogger Log = LogManager.GetLogger("Common");

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
