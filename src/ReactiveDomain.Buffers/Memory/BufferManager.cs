using System;
using System.Collections.Generic;
using ReactiveDomain.Buffers.Examples;
using ReactiveDomain.Logging;

namespace ReactiveDomain.Buffers.Memory
{
    public sealed class BufferManager : IDisposable
    {
        private static readonly ILogger Log = LogManager.GetLogger("Common");

        private readonly List<PinnedBuffer> _buffers;
        public string Name { get; }

        public BufferManager(string name)
        {
            Name = name;
            _buffers = new List<PinnedBuffer>();
            Log.Debug(Name + " Buffer collection initialized - there are now 0 buffers total.");
        }

        public WrappedBuffer GetBuffer(long size)
        {
            return new WrappedBuffer(this, CheckOutBuffer(size));
        }
        /// <summary>
        /// for example
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public WrappedImage<T> GetFramedImage<T>() where T: Image ,new()
        {
            var frameType = new T();
            return new WrappedImage<T>(this, CheckOutBuffer(frameType.BufferSize));
        }
        
        private PinnedBuffer CheckOutBuffer(long size)
        {
            var buffer = FirstAvailableBuffer(size);
            if (buffer == null)
            {
                buffer = new PinnedBuffer(size);
                _buffers.Add(buffer);
            }
            buffer.Reserve(); //todo: improve encapsulation here
            Log.Debug(Name + " Buffer of size " + size +
                " checked out - there are now " + Count(b => b.Length == size) +
                " buffers of that size, " + Count(b => b.Length == size && b.Available) + " available.");
            return buffer;
        }

        /// <summary>
        /// Finds the first available buffer or returns null. Calls to linq
        /// like FirstOrDefault are not thread safe and blow up if the underlying
        /// iterator changes.
        /// </summary>
        private PinnedBuffer FirstAvailableBuffer(long size)
        {
            PinnedBuffer buffer = null;
            // This implementation assumes that we add to _buffers, but don't remove
            for (int i = 0; i < _buffers.Count; i++)
            {
                var b = _buffers[i];
                if (b!= null && b.Length == size && b.Available)
                {
                    buffer = b;
                    break;
                }
            }

            return buffer;
        }

        private int Count(Predicate<PinnedBuffer> test)
        {
            int count = 0;
            // This implementation assumes that we add to _buffers, but don't remove
            for (int i = 0; i < _buffers.Count; i++)
            {
                var b = _buffers[i];
                if (b != null && test(b))
                {
                    count++;
                }
            }

            return count;
        }

        internal void Return(PinnedBuffer buffer)
        {
            buffer.Release(); //todo: improve encapsulation here
            Log.Debug(Name + " Buffer returned.");
        }

        public void Dispose()
        {
            foreach (var buffer in _buffers)
            {
                buffer.Dispose();
            }
            Log.Debug(Name + " Buffer collection now disposed.");
        }
    }
}
