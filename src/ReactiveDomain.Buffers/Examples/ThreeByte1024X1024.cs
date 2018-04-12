using System.Runtime.InteropServices;
using ReactiveDomain.Logging;

namespace ReactiveDomain.Buffers.Examples
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ThreeByte1024X1024Frame
    {
        public FrameHeader FrameHeader;
        public fixed byte pixels[3*1024*1024];
    }
    public unsafe class ThreeByte1024X1024Image : Image
    {
        private static readonly ILogger Log = LogManager.GetLogger("Common");

        public const int PixelHeight = 1024;
        public const int PixelWidth = 1024;
        public const int Rgb24Bytes = 3;

        //n.b this should only be be called by generic or refection constructors
        public ThreeByte1024X1024Image() {
            
        }
        public ThreeByte1024X1024Image(byte* buffer)
            : base(buffer, sizeof(ThreeByte1024X1024Frame))
        {

        }

        public override byte* PixelBuffer
        {
            get
            {
                CheckLifetime();
                return ((ThreeByte1024X1024Frame*)Buffer)->pixels;
            }
        }

        public static int PixelBufferLength => 3 * 1024 * 1024;

    }
}