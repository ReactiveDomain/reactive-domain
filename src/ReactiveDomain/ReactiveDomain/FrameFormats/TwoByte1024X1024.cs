using System;
using System.Runtime.InteropServices;
using NLog;
using ReactiveDomain.Memory;
using ReactiveDomain.Util;

namespace ReactiveDomain.FrameFormats
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct TwoByte1024X1024Frame
    {
        public long FrameNumber;
        public fixed byte FrameId[16];
        public fixed byte VideoId[16];
        public double OffsetMilliseconds;
        public fixed byte pixels[2*1024*1024];
    }
    public unsafe class TwoByte1024X1024Image : Image
    {
        private static readonly Logger Log = NLog.LogManager.GetLogger("Common");

        public TwoByte1024X1024Image(byte* buffer)
            : base(buffer, sizeof(TwoByte1024X1024Frame))
        {
        }

        public TwoByte1024X1024Image()
        {
            BufferSize = sizeof(TwoByte1024X1024Frame);
        }

        /// <summary>
        /// Gets a pointer to the pixel buffer.
        /// </summary>
        public override byte* PixelBuffer
        {
            get
            {
                CheckLifetime();
                return ((TwoByte1024X1024Frame*)Buffer)->pixels;
            }
        }

        public static int PixelBufferLength => 2 * 1024 * 1024;

        /// <summary>
        /// Gets or sets the number of this frame within the video.
        /// </summary>
        public override long FrameNumber
        {
            get
            {
                CheckLifetime();
                return ((TwoByte1024X1024Frame*)Buffer)->FrameNumber;
            }
            set
            {
                CheckLifetime();
                Ensure.Positive(value, "FrameNumber");
                ((TwoByte1024X1024Frame*)Buffer)->FrameNumber = value;
            }
        }

        /// <summary>
        /// Gets or sets the unique ID of this video associated with this image.
        /// </summary>
        public override Guid VideoId
        {
            get
            {
                CheckLifetime();
                return Utility.ParseGuidBuffer(((TwoByte1024X1024Frame*)Buffer)->VideoId);
            }
            set
            {
                CheckLifetime();
                if (value == Guid.Empty)
                {
                    Log.Error("VideoId cannot be an empty Guid.");
                    throw new ArgumentException("VideoId cannot be an empty Guid.");
                }
                value.CopyToBuffer(((TwoByte1024X1024Frame*)Buffer)->VideoId);
            }

        }

        /// <summary>
        /// Gets or sets the unique ID for this frame.
        /// </summary>
        public override Guid FrameId
        {
            get
            {
                CheckLifetime();
                return Utility.ParseGuidBuffer(((TwoByte1024X1024Frame*)Buffer)->FrameId);
            }
            set
            {
                CheckLifetime();
                if (value == Guid.Empty)
                {
                    Log.Error("FrameId cannot be an empty Guid.");
                    throw new ArgumentException("FrameId cannot be an empty Guid.");
                }
                value.CopyToBuffer(((TwoByte1024X1024Frame*)Buffer)->FrameId);
            }
        }

        /// <summary>
        /// Gets or sets the offset in milliseconds of this frame from the beginning of the video.
        /// </summary>
        public override double Offset
        {
            get
            {
                CheckLifetime();
                return ((TwoByte1024X1024Frame*)Buffer)->OffsetMilliseconds;
            }
            set
            {
                CheckLifetime();
                if (value < 0)
                {
                    Log.Error("Offset must be a positive value.");
                    throw new ArgumentException("Offset must be a positive value.");
                }
                ((TwoByte1024X1024Frame*)Buffer)->OffsetMilliseconds = value;
            }
        }
    }
}
