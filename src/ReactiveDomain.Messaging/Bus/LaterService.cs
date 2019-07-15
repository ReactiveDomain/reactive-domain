using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ReactiveDomain.Messaging.Bus {
    public sealed class LaterService : IHandle<DelaySendEnvelope>, IDisposable {
        private readonly IPublisher _outbound;
        private readonly ITimeSource _timeSource;
        private readonly SortedList<TimePosition, List<IMessage>> _pending;
        private readonly ConcurrentQueue<DelaySendEnvelope> _inbound;
        private const long True = 1;
        private const long False = 0;
        private readonly ManualResetEventSlim _processNext;
        private long _stop = False;
        private long _stopped = False;

        private Thread _thread;

        public LaterService(IPublisher outbound, ITimeSource timeSource) {
            _outbound = outbound;
            _timeSource = timeSource;
            _inbound = new ConcurrentQueue<DelaySendEnvelope>();
            //we can use a simple sorted list here because it is only accessed on the processing thread
            _pending = new SortedList<TimePosition, List<IMessage>>();
            _processNext = new ManualResetEventSlim(true);
        }

        public void Start() {
            if (_disposed) { throw new ObjectDisposedException(nameof(LaterService)); }
            var thread = new Thread(Process) { Name = nameof(LaterService), IsBackground = true };
            if (Interlocked.CompareExchange(ref _thread, thread, null) != null) throw new InvalidOperationException("Already started");
            _stop = False;
            _stopped = False;
            thread.Start();
        }
        /// <summary>
        /// Stops the Processing Queue 
        /// </summary>
        /// <param name="timeoutInMilliseconds">
        /// -1: Infinite wait to stop
        /// 0-N: throw if not stopped by N milliseconds 
        /// </param>
        public void Stop(int timeoutInMilliseconds) {
            if (Interlocked.CompareExchange(ref _stop, True, False) != False) { return; }
            _processNext.Set();
            if (!SpinWait.SpinUntil(() => Interlocked.Read(ref _stopped) == True, timeoutInMilliseconds)) {
                throw new TimeoutException();
            }
            _thread = null;
        }

        public void Handle(DelaySendEnvelope msg) {
            if (Interlocked.Read(ref _stop) == True || _disposed) { return; }
            //using an inbound queue and processing it in the loop is about 300% faster than using any other concurrent structure and trying to scan
            //it for expired messages

            _inbound.Enqueue(msg);//place even expired items in the pending queue for consistent thread usage
            _processNext.Set();
        }

        private void Process() {
            while (Interlocked.Read(ref _stop) == False) {
                SendExpired(_timeSource);
                ProcessInbound();

                if (_pending.Count > 0) {//wait for next queued item or inbound msg
                    _timeSource.WaitFor(_pending.Keys[0], _processNext);
                }
                else { //nothing queued, wait for inbound msg
                    _processNext.Wait();
                }
                _processNext.Reset();
            }
            Interlocked.Exchange(ref _stopped, True);
        }

        private void ProcessInbound() {
            if (_disposed) { return; }
            while (_inbound.TryDequeue(out var msg) && Interlocked.Read(ref _stop) == False) {
                //place even expired items in the pending queue for consistent thread usage
                if (!_pending.TryGetValue(msg.At, out var list)) {
                    list = new List<IMessage>();
                    _pending[msg.At] = list;
                }
                list.Add(msg.ToSend);
            }
        }

        private void SendExpired(ITimeSource timeSource) {
            if (_disposed) { return; }
            if (_pending.Count <= 0) { return; }
            do {
                var timeKey = _pending.Keys.FirstOrDefault();
                if (timeKey is null) { return; }
                if (timeKey > timeSource.Now()) { return; }
                var toDispatch = _pending[timeKey];
                _pending.RemoveAt(0);
                for (int i = 0; i < toDispatch.Count; i++) {
                    if (Interlocked.Read(ref _stop) == True) { return; }
                    _outbound.Publish(toDispatch[i]);
                }
            } while (_pending.Count <= 0);
        }

        private bool _disposed;
        public void Dispose() {
            if (_disposed) { return; }
            _disposed = true;
            try { Stop(100); } catch {/*don't throw exceptions here, but don't wait forever either */}
            _pending.Clear();
            while (_inbound.TryDequeue(out _)) { /* empty the queue */ }
            _processNext?.Dispose();
#if NETFRAMEWORK
            if (_thread?.IsAlive ?? false) { _thread?.Abort(); }
#endif
        }
    }
#if NET452 || NET40
    //copied from from .net framework 472 ToUnixTimeMilliseconds implementation
    //added here to support 452
    public static class TimeHelper {
        /// <summary>Returns the number of milliseconds that have elapsed since 1970-01-01T00:00:00.000Z. </summary>
        /// <returns>The number of milliseconds that have elapsed since 1970-01-01T00:00:00.000Z. </returns>
        public static long ToUnixTimeMilliseconds(this DateTimeOffset dt) {
            return dt.UtcDateTime.Ticks / 10000L - 62135596800000L;
        }
    }
#endif
}