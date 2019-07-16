using System;
using ReactiveDomain.Buffers.Memory;
using Xunit;

namespace ReactiveDomain.Buffers.Tests
{
    public class BufferManagerTests
    {
        [Fact]
        public unsafe void can_get_new_buffer()
        {
            var mgr = new BufferManager("test");
            var buf1 = mgr.GetBuffer(30);
            Assert.NotNull(buf1);
            var source = new byte[30];
            for (int i = 0; i < 30; i++)
            {
                source[i] = (byte)(i % 8);
            }
            fixed (byte* s = source)
            {
                Utility.CopyMemory(buf1.Buffer, s, 30); // fill the whole buffer
                Assert.True(BufferTests.BufferEqual(s, buf1.Buffer, 30));
            }
        }

        [Fact]
        public unsafe void new_buffers_are_different()
        {
            var mgr = new BufferManager("test");
            var buf1 = mgr.GetBuffer(30);
            var buf2 = mgr.GetBuffer(30);
            Assert.NotEqual((long)buf1.Buffer, (long)buf2.Buffer);
            var buf3 = mgr.GetBuffer(40);
            Assert.NotEqual((long)buf1.Buffer, (long)buf3.Buffer);
            Assert.NotEqual((long)buf2.Buffer, (long)buf3.Buffer);
        }

        [Fact]
        public unsafe void returned_buffers_are_empty_and_can_be_reused()
        {
            var mgr = new BufferManager("test");
            var buf1 = mgr.GetBuffer(30);
            Assert.NotNull(buf1);
            var source = new byte[30];
            for (int i = 0; i < 30; i++)
            {
                source[i] = (byte)(i % 8);
            }

            fixed (byte* s = source)
            {
                Utility.CopyMemory(buf1.Buffer, s, 30); // fill the whole buffer
                Assert.True(BufferTests.BufferEqual(s, buf1.Buffer, 30));
            }
            var empty = new byte[30];
            var addr = (long)buf1.Buffer;
            buf1.Dispose();
            buf1 = mgr.GetBuffer(30);
            Assert.Equal(addr, (long)buf1.Buffer);
            fixed (byte* e = empty)
            {
                Assert.True(BufferTests.BufferEqual(e, buf1.Buffer, 30));
            }
        }

        [Fact]
        public void all_buffers_are_released_by_manager()
        {
            var mgr = new BufferManager("test");
            var buf1 = mgr.GetBuffer(30);
            var buf2 = mgr.GetBuffer(30);
            var buf3 = mgr.GetBuffer(40);

            mgr.Dispose();
            Assert.Throws<ObjectDisposedException>(() => buf1.Length);
            Assert.Throws<ObjectDisposedException>(() => buf2.Length);
            Assert.Throws<ObjectDisposedException>(() => buf3.Length);
        }

        [Fact]
        public unsafe void can_align_memory()
        {
            byte[] foo = new byte[55 + 8];
            byte[] source = new byte[55];
            var rnd = new Random(DateTime.Now.Millisecond);
            rnd.NextBytes(source);

            fixed (byte* f = foo)
            fixed (byte* s = source)
            {
                Assert.True(((long)f) % 8 == 0);
                for (int i = 0; i < 8; i++)
                {
                    var l = Utility.AlignTo16(f + 1);
                    Assert.True(((long)l) % 16 == 0);
                    var sourcePtr = (IntPtr)s;
                    var destPtr = (IntPtr)l;
                    Utility.CopyMemory(destPtr, sourcePtr, 55);
                    Assert.True(BufferTests.BufferEqual(l, s, 55));
                }
            }

        }

    }
}
