using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using ReactiveDomain.Buffers.Examples;
using ReactiveDomain.Logging;

namespace ReactiveDomain.Buffers.FileIO
{
    public class BufferWriter: IDisposable
    {
        internal class PendingBuffer
        {
            public readonly FileInfo Target;
            public readonly VideoSlice Buffer;

            public PendingBuffer(FileInfo target, VideoSlice buffer)
            {
                Target = target;
                Buffer = buffer;
            }
        }
        private static readonly ILogger Log = LogManager.GetLogger("Storage");

        private readonly ConcurrentQueue<PendingBuffer> _queue = new ConcurrentQueue<PendingBuffer>();
        private readonly ManualResetEventSlim _msgAddEvent = new ManualResetEventSlim(false);

        private readonly Thread _thread;
        private volatile bool _starving;
        private bool _stopped;

        public bool HasPendingWrites { get { return !_starving; } }

        public BufferWriter()
        {
            if(!Environment.Is64BitProcess) throw new NotSupportedException("Buffer reading and writing from disk require an x64 process");
            _thread = new Thread(ProcessQueue) { IsBackground = true, Name = "BufferWriter", Priority = ThreadPriority.AboveNormal };
            _thread.Start();
        }

        internal void EnqueueBuffer(FileInfo target, VideoSlice buffer)
        {
            _starving = false;
            _queue.Enqueue(new PendingBuffer (target, buffer));
            Log.Trace("Enqueued buffer for " + target.Name + " - queue now contains " + _queue.Count + " buffers.");
            _msgAddEvent.Reset();
        }

        private void ProcessQueue(object o)
        {

            while (!_stopped)
            {
                PendingBuffer buffer = null;
                try
                {
                    if (!_queue.TryDequeue(out buffer))
                    {
                        _starving = true;

                        _msgAddEvent.Wait(100);
                        _msgAddEvent.Reset();

                        _starving = false;
                    }
                    else
                    {
                        Log.Trace("Dequeued buffer for " + buffer.Target.Name + " - queue now contains " + _queue.Count + " buffers.");
                        if (!TryWriteBufferToDisk(buffer.Target, buffer.Buffer))
                        {
                            Log.Error($"Error while flushing buffer to {buffer.Target.FullName}.");
                        }
                    }

                }
                catch (Exception ex)
                {
                    Log.ErrorException(ex,
                        buffer != null
                            ? $"Error while flushing buffer to {buffer.Target.FullName}."
                            : "buffer Writer Error.");
                }
            }
        }

        internal unsafe bool TryWriteBufferToDisk(FileInfo target, VideoSlice buffer)
        {
            Log.Trace("Attempting to write a frame buffer to '" + target.Name + "'");
            if (!UnbufferedFileTools.TryWrite(
                target,
                (int) buffer.BufferSize,
                buffer.AsBytePtr)
                )
            {
                Log.Error("UnbufferedFileTools.TryWrite(" + target.Name + ") failed.");
                return false;
            }
            // Now that the buffer contents have been written to disk, clear the buffer frames for the
            // next set of contents.
            buffer.TryReset();
            buffer.Unlock();
            return true;
        }

        internal unsafe bool TryLoadBufferFromDisk(FileInfo target, VideoSlice buffer)
        {
            return buffer.TryReset() &&
                   UnbufferedFileTools.TryRead(
                                            target,
                                            (int)buffer.BufferSize,
                                            buffer.AsBytePtr
                                            );
        }


        public void Dispose()
        {
            _stopped = true;
            _msgAddEvent.Reset();
            _thread.Join();
            _msgAddEvent.Dispose();
        }
    }
}
