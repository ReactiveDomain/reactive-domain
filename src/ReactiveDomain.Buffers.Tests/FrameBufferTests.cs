using System;
using ReactiveDomain.Buffers.Examples;
using ReactiveDomain.Buffers.Memory;
using Xunit;

namespace ReactiveDomain.Buffers.Tests
{
    public class FrameBufferTests
    {
        private readonly BufferManager _buffermanager;
        private readonly Func<long, WrappedBuffer> _getBuffer;
        public FrameBufferTests()
        {
            _buffermanager = new BufferManager("test");
            _getBuffer = _buffermanager.GetBuffer;
        }

        private readonly Random _rnd = new Random(DateTime.Now.Millisecond);
        private byte[][] BuildRandomFrames(uint count, uint size)
        {
            var frames = new byte[count][];
            for (int i = 0; i < count; i++)
            {
                var frame = new byte[size];
                _rnd.NextBytes(frame);
                frames[i] = frame;
            }
            return frames;
        }
        [Fact]
        public unsafe void can_create_frame_buffer_with_specified_count_and_size()
        {
            const uint count = 5u;
            uint size = (uint)sizeof(TwoByte1024X1024Frame);
            var buffer = (VideoSlice)new MonoVideoSlice(count,  _getBuffer);
            var frames = BuildRandomFrames(count, size);

            Assert.Equal(buffer.Count, count);
            Assert.Equal(buffer.FrameSize, size);
            Assert.Equal(buffer.BufferSize, (size * count) + sizeof(VideoSliceHeader));
            Assert.NotEqual(buffer.Id, Guid.Empty);
            Console.WriteLine("Hi!");
            for (uint i = 0; i < buffer.Count; i++)
            {
                fixed (byte* f = frames[i])
                {
                    Utility.CopyMemory(buffer[i], f, size);
                }
            }
            for (uint frameIndex = 0; frameIndex < buffer.Count; frameIndex++)
            {
                byte* bufferFrame = buffer[frameIndex];
                fixed (byte* sourceFrame = frames[frameIndex])
                    for (uint b = 0; b < size; b++)
                    {
                        Assert.Equal(sourceFrame[b], bufferFrame[b]);
                    }
            }
        }

        [Fact]
        [Trait("Duration", "LongRunning")]
        public unsafe void can_copy_buffer()
        {
            const uint count = 20u;
            uint size = (uint)sizeof(TwoByte1024X1024Frame);
            var buffer = (VideoSlice)new MonoVideoSlice(count, _getBuffer);
            var frames = BuildRandomFrames(count, size);
            for (uint i = 0; i < buffer.Count; i++)
            {
                fixed (byte* f = frames[i])
                {
                    Utility.CopyMemory(buffer[i], f, size);
                }
            }
            var buffer2 = (VideoSlice)new MonoVideoSlice(count, _getBuffer);
            buffer2.CopyFrom(buffer);
            Assert.Equal(buffer.Id, buffer2.Id);
            for (uint frameIndex = 0; frameIndex < buffer2.Count; frameIndex++)
            {
                byte* bufferFrame = buffer2[frameIndex];
                fixed (byte* sourceFrame = frames[frameIndex])
                {
                    Assert.True(BufferTests.BufferEqual(bufferFrame, sourceFrame, (int)size));
                }
            }

        }
        [Fact]
        public unsafe void can_copy_frames()
        {
            const uint count = 20u;
            uint size = (uint)sizeof(TwoByte1024X1024Frame);
            var buffer = (VideoSlice)new MonoVideoSlice(count, _getBuffer);
            var frames = BuildRandomFrames(count, size);
            for (uint i = 0; i < buffer.Count; i++)
            {
                fixed (byte* f = frames[i])
                {
                    Utility.CopyMemory(buffer[i], f, size);
                }
            }
            var buffer2 =  (VideoSlice)new MonoVideoSlice(count, _getBuffer);

            Utility.CopyMemory(buffer2.FramesBuffer, buffer.FramesBuffer, buffer.FramesBufferSize);
            Assert.NotEqual(buffer.Id, buffer2.Id);
            for (uint frameIndex = 0; frameIndex < buffer2.Count; frameIndex++)
            {
                byte* bufferFrame = buffer2[frameIndex];
                fixed (byte* sourceFrame = frames[frameIndex])
                {
                    var equal = BufferTests.BufferEqual(bufferFrame, sourceFrame, (int) size);
                    Assert.True(equal);
                }
            }

        }


        [Fact]
        public unsafe void can_reset_framebuffer()
        {
            const uint count = 20;
            uint size = (uint)sizeof(TwoByte1024X1024Frame);
            var buffer = (VideoSlice)new MonoVideoSlice(count, _getBuffer);
            var frames = BuildRandomFrames(count, size);

            for (uint i = 0; i < buffer.Count; i++)
            {
                fixed (byte* f = frames[i])
                {
                    Utility.CopyMemory(buffer[i], f, size);
                }
            }
            var oldId = buffer.Id;
            var oldCount = buffer.Count;
            var oldFrameSize = buffer.FrameSize;
            Assert.True(buffer.TryReset());

            Assert.NotEqual(oldId, buffer.Id);
            Assert.Equal(oldCount, buffer.Count);
            Assert.Equal(oldFrameSize, buffer.FrameSize);
            var empty = new byte[size];
            for (uint frameIndex = 0; frameIndex < buffer.Count; frameIndex++)
            {
                fixed (byte* e = empty)
                {
                    Assert.True(BufferTests.BufferEqual(e, buffer[frameIndex], (int)size));
                }
            }
        }
     
    }
}