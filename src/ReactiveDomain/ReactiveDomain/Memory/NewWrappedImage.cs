using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveDomain.FrameFormats;
using ReactiveDomain.Util;

namespace ReactiveDomain.Memory
{
    public class NewWrappedImage : WrappedBuffer
    {
        public unsafe NewWrappedImage(BufferManager manager, PinnedBuffer buffer, int dimension, int bytesPerPixel) :
            base(manager, buffer)
        {
            switch (bytesPerPixel)
            {
                case 2:
                {
                    switch (dimension)
                    {
                        case 128:
                            Frame = new TwoByte128X128Image(buffer.Buffer);
                            break;
                        case 256:
                            Frame = new TwoByte256X256Image(buffer.Buffer);
                            break;

                        case 512:
                            Frame = new TwoByte512X512Image(buffer.Buffer);
                            break;
                        case 1024:
                            Frame = new TwoByte1024X1024Image(buffer.Buffer);
                            break;
                        default:
                            Frame = null;
                            break;
                    }
                    break;
                }

                case 3:
                    switch (dimension)
                    {
                        case 1024:
                            Frame = new ThreeByte1024X1024Image(buffer.Buffer);
                            break;
                        default:
                            Frame = null;
                            break;
                    }
                    break;
                default:
                    Frame = null;
                    break;
            }
            ImagePixelHeight = dimension;
            ImagePixelWidth = dimension;
            BytesPerPixel = bytesPerPixel;
        }

        public Image Frame { get; private set; }
        public int ImagePixelHeight { get; private set; }
        public int ImagePixelWidth { get; private set; }
        public int BytesPerPixel { get; private set; }
        public unsafe byte* PixelBuffer => Frame.PixelBuffer;
    }
}
