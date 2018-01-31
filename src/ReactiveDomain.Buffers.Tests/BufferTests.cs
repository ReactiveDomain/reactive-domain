using System;
using ReactiveDomain.Buffers.Memory;
using Xunit;

namespace ReactiveDomain.Buffers.Tests
{
    public class BufferTests
    {
        /// <summary>
        /// Buffer equality function 
        /// Length must be equal to the length of the buffer in bytes
        /// </summary>
        /// <param name="b1">First buffer to compare</param>
        /// <param name="b2">Second buffer to compare</param>
        /// <returns></returns>
        public static unsafe bool BufferEqual(byte[] b1, byte[] b2)
        {
            if (b1 == null || b2 == null || b1.Length != b2.Length)
                return false;
            fixed (byte* p1 = b1, p2 = b2)
                return BufferEqual(p1, p2, b1.Length);
        }
        public static unsafe bool BufferEqual(byte* b1, byte* b2, int length)
        {
            if (b1 == b2) return true;
            
            for (var i = 0; i < length / 8; i++, b1 += 8, b2 += 8)
                if (*((long*)b1) != *((long*)b2)) 
                    return false;

            for (int i = 0; i < length % 8; i++, b1++, b2++)
                if (*b1 != *b2) 
                    return false;

            return true;
        }
        
        private static byte[] GetRndFilledBuffer(int length)
        {
            var rnd = new Random(DateTime.Now.Millisecond);
            var buffer = new byte[length];
            rnd.NextBytes(buffer);
            return buffer;
        }
        [Fact]
        public unsafe void buffers_equal_themselves()
        {
            const int length = 64;
            var buffer1 = GetRndFilledBuffer(length);
            var buffer2 = buffer1;
           

            fixed (byte* b1 = buffer1)
            fixed (byte* b2 = buffer1)
            {
                Assert.True(BufferEqual(buffer1, buffer2));
                Assert.True(BufferEqual(b1, b2, length));
            }
        }
        [Fact]
        public unsafe void buffer_is_equal_for_mulitples_of_64()
        {
            const int length = 64 * 120;
            var buffer1 = GetRndFilledBuffer(length);
            var buffer2 = new byte[length];
            for (int i = 0; i < length; i++)
            {
                buffer2[i] = buffer1[i];
            }
           
            fixed (byte* b1 = buffer1)
            fixed (byte* b2 = buffer2)
            {
                Assert.True(BufferEqual(buffer1, buffer2));
                Assert.True(BufferEqual(b1, b2, length));
            }
        }
        [Fact]
        public unsafe void buffer_is_equal_for_non_mulitples_of_64()
        {
            const int length = (64 * 120) + 5;
            var buffer1 = GetRndFilledBuffer(length);
            var buffer2 = new byte[length];
            for (int i = 0; i < length; i++)
            {
                buffer2[i] = buffer1[i];
            }
            
            fixed (byte* b1 = buffer1)
            fixed (byte* b2 = buffer2)
            {
                Assert.True(BufferEqual(buffer1, buffer2));
                Assert.True(BufferEqual(b1, b2, length));
            }
        }
        [Fact]
        public unsafe void buffer_is_not_equal_for_mulitples_of_64()
        {
            const int length = 64 * 120;
            var buffer1 = GetRndFilledBuffer(length);
            var buffer2 = GetRndFilledBuffer(length);
            for (int i = 0; i < length; i++)
            {
                buffer2[i] = buffer1[i];
            }
            
            
            fixed (byte* b1 = buffer1)
            fixed (byte* b2 = buffer2)
            {
                buffer1[0] = 0;
                buffer2[0] = 1;

                Assert.False(BufferEqual(buffer1, buffer2));
                Assert.False(BufferEqual(b1, b2, length));

                buffer1[0] = 0;
                buffer2[0] = 0;
                buffer1[length / 2] = 0;
                buffer2[length / 2] = 1;

                Assert.False(BufferEqual(buffer1, buffer2));
                Assert.False(BufferEqual(b1, b2, length));

                buffer1[length / 2] = 0;
                buffer2[length / 2] = 0;
                buffer1[length - 1] = 0;
                buffer2[length - 1] = 1;

                Assert.False(BufferEqual(buffer1, buffer2));
                Assert.False(BufferEqual(b1, b2, length));
            }


        }
        [Fact]
        public unsafe void buffer_is_not_equal_for_non_mulitples_of_64()
        {
            const int length = (64 * 120) + 5;
            var buffer1 = GetRndFilledBuffer(length);
            var buffer2 = GetRndFilledBuffer(length);
            for (int i = 0; i < length; i++)
            {
                buffer2[i] = buffer1[i];
            }
          
            fixed (byte* b1 = buffer1)
            fixed (byte* b2 = buffer2)
            {
                buffer1[0] = 0;            
                buffer2[0] = 1;

                Assert.False(BufferEqual(buffer1, buffer2));
                Assert.False(BufferEqual(b1, b2, length));

                buffer1[0] = 0;
                buffer2[0] = 0;
                buffer1[length / 2] = 0;
                buffer2[length / 2] = 1;

                Assert.False(BufferEqual(buffer1, buffer2)); 
                Assert.False(BufferEqual(b1, b2, length));

                buffer1[length / 2] = 0;
                buffer2[length / 2] = 0;
                buffer1[length - 1] = 0;
                buffer2[length - 1] = 1;

                Assert.False(BufferEqual(buffer1, buffer2)); 
                Assert.False(BufferEqual(b1, b2, length));
            }
        }

        [Fact]
        public unsafe void can_parse_guid_buffers()
        {

            var g = Guid.NewGuid();
            Guid g2;
            var bytes = g.ToByteArray();
            fixed (byte* b = bytes)
            {
                g2 = Utility.ParseGuidBuffer(b);
            }
            Assert.Equal(g, g2);
        }

        [Fact]
        public unsafe void can_save_guid_to_byte_buffer()
        {
            var g = Guid.NewGuid();
            Guid g2;
            var buffer = new byte[16];
            fixed (byte* b = buffer)
            {
                g.CopyToBuffer(b);
                g2 = Utility.ParseGuidBuffer(b);
            }
            Assert.Equal(g, g2);
        }
        [Fact]
        public unsafe void empty_buffer_is_empty_guid()
        {
            Guid g2;
            var buffer = new byte[16];
            fixed (byte* b = buffer)
            {

                g2 = Utility.ParseGuidBuffer(b);
            }
            Assert.Equal(Guid.Empty, g2);
        }

     
    }
}