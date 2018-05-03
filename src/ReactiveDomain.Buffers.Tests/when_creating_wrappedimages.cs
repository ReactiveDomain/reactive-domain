using ReactiveDomain.Buffers.Memory;

namespace ReactiveDomain.Buffers.Tests
{
    // ReSharper disable once InconsistentNaming
    public class when_creating_wrappedimages
    {
        protected BufferManager BufferMgr = new BufferManager("Image Tests");
         /* [Fact(Skip="Add Wrapped Image example test")]
        public unsafe void can_create_wrappedimages()
        {
            //N.B. was based on remove square image method on Buffer manager
            //TODO: re-implement for GetFramedImage
          
            int dimension = 1024;
            int bytesPerPixel = 2;
            var wrapped = BufferMgr.GetFramedImage<ThreeByte1024X1024Image>();

            var frame = (ThreeByte1024X1024Frame*)wrapped.Buffer;


            //Assert.Equal((1024*1024*3) + sizeof(FrameHeader),wrapped.Frame.BufferSize);
            //Assert.Equal(image1024.ImagePixelWidth, dimension);
            //Assert.Equal(image1024.BytesPerPixel, bytesPerPixel);
            //Assert.Equal(image1024.PixelBufferLength, dimension * dimension * bytesPerPixel );

            //TODO: fix or remove
            //these don't seem to play well with run all
            //not sure what they are proving either

            byte* pByte = image1024.PixelBuffer + dimension;
            *pByte = 255;
            Assert.Equal(*pByte, 255);
            Assert.NotEqual(*(pByte + 1), 255);

            pByte = image1024.PixelBuffer + (dimension * dimension);
            *pByte = 255;
            Assert.Equal(*pByte, 255);
            Assert.NotEqual(*(pByte + 1), 255);

            pByte = image1024.PixelBuffer + image1024.PixelBufferLength;
            *pByte = 255;
            Assert.Equal(*pByte, 255);
            Assert.NotEqual(*(pByte - 1), 255); //let's not go off the end here

            dimension = 512;
            WrappedSquareImage image512 = BufferMgr.GetWrappedSquareImage(dimension, bytesPerPixel);

            Assert.Equal(image512.ImagePixelHeight, dimension);
            Assert.Equal(image512.ImagePixelWidth, dimension);
            Assert.Equal(image512.BytesPerPixel, bytesPerPixel);
            Assert.Equal(image512.PixelBufferLength, dimension * dimension * bytesPerPixel);

            dimension = 256;
            WrappedSquareImage image256 = BufferMgr.GetWrappedSquareImage(dimension, bytesPerPixel);

            Assert.Equal(image256.ImagePixelHeight, dimension);
            Assert.Equal(image256.ImagePixelWidth, dimension);
            Assert.Equal(image512.BytesPerPixel, bytesPerPixel);
            Assert.Equal(image256.PixelBufferLength, dimension * dimension * bytesPerPixel);

            dimension = 128;
            WrappedSquareImage image128 = BufferMgr.GetWrappedSquareImage(dimension, bytesPerPixel);

            Assert.Equal(image128.ImagePixelHeight, dimension);
            Assert.Equal(image128.ImagePixelWidth, dimension);
            Assert.Equal(image128.BytesPerPixel, bytesPerPixel);
            Assert.Equal(image128.PixelBufferLength, dimension * dimension * bytesPerPixel);

            dimension = 1024;
            bytesPerPixel = 3;
            image1024 = BufferMgr.GetWrappedSquareImage(dimension, bytesPerPixel);

            Assert.Equal(image1024.ImagePixelHeight, dimension);
            Assert.Equal(image1024.ImagePixelWidth, dimension);
            Assert.Equal(image1024.BytesPerPixel, bytesPerPixel);
            Assert.Equal(image1024.PixelBufferLength, dimension * dimension * bytesPerPixel);
            
        }*/
    }
}
