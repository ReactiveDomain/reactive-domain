using ReactiveDomain.Buffers.Examples;

namespace ReactiveDomain.Buffers.Memory
{
    public class WrappedImage<T> : WrappedBuffer where T : Image, new()
    {
        public unsafe WrappedImage(BufferManager manager, PinnedBuffer buffer) :
            base(manager, buffer)
        {
            Frame = new T();
            Frame.SetBuffer(buffer.Buffer, (int)buffer.Length);
        }

        public T Frame { get; private set; }
    }
}
