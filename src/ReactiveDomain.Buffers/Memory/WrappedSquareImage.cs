using System;
using ReactiveDomain.Buffers.FrameFormats;

namespace ReactiveDomain.Buffers.Memory
{
    public class WrappedSquareImage : WrappedBuffer
    {
        public unsafe WrappedSquareImage(BufferManager manager, PinnedBuffer buffer, int dimension, int bytesPerPixel) :
            base(manager, buffer)
        {
            switch (dimension)
            {
                case 128:
                InnerImage = new TwoByte128X128Image(buffer.Buffer);
                break;
                case 256:
                InnerImage = new TwoByte256X256Image(buffer.Buffer);
                break;
                case 512:
                InnerImage = new TwoByte512X512Image(buffer.Buffer);
                break;
                case 1024:
                {
                    if (bytesPerPixel == 2)
                        InnerImage = new TwoByte1024X1024Image(buffer.Buffer);
                    else if (bytesPerPixel == 3)
                        InnerImage = new ThreeByte1024X1024Image(buffer.Buffer);
                    else
                        throw new Exception($"Invalid bytes per pixel requested for new WrappedSquareImage: {bytesPerPixel}");

                    break;
                }
                default:
                    throw new Exception($"Invalid image size requested for new WrappedSquareImage: {dimension}");
            }

            ImagePixelHeight = dimension;
            ImagePixelWidth = dimension;
            BytesPerPixel = bytesPerPixel;
        }

        public int ImagePixelHeight { get; private set; }
        public int ImagePixelWidth { get; private set; }
        public int BytesPerPixel { get; private set; }
        public int PixelBufferLength => (ImagePixelHeight * ImagePixelWidth * BytesPerPixel);

        public unsafe byte* PixelBuffer => InnerImage.PixelBuffer;

        private Image InnerImage { get; }
    }
}
