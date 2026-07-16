using System.Collections.Concurrent;
using ReactiveDomain.Logging;

namespace ReactiveDomain.Messaging.Bus;

public sealed class LaterService : IHandle<DelaySendEnvelope>, IDisposable {
	private static readonly ILogger _log = LogManager.GetLogger("ReactiveDomain");
	private readonly IPublisher _outbound;
	private readonly ITimeSource _timeSource;
	private readonly SortedList<TimePosition, List<IMessage>> _pending;
	private readonly ConcurrentQueue<DelaySendEnvelope> _inbound;
	private readonly ManualResetEventSlim _processNext;
	private readonly object _stopLock = new();
	private readonly object _handleLock = new();
	private volatile bool _stop;
	private volatile bool _stopped;

	private Thread? _thread;

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
		if (Interlocked.CompareExchange(ref _thread, thread, null) != null)
			throw new InvalidOperationException("Already started");
		_stop = false;
		_stopped = false;
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
		lock (_stopLock) {
			if (_stop) { return; }
			_stop = true;
		}
		_processNext.Set();
		if (!SpinWait.SpinUntil(() => _stopped, timeoutInMilliseconds)) {
			throw new TimeoutException();
		}
		_thread = null;
	}

	public void Handle(DelaySendEnvelope msg) {
		if (_stop || _disposed) { return; }
		//using an inbound queue and processing it in the loop is about 300% faster than using any other concurrent structure and trying to scan
		//it for expired messages

		_inbound.Enqueue(msg);//place even expired items in the pending queue for consistent thread usage
		_processNext.Set();
	}

	private void Process() {
		while (!_stop) {
			try {
				SendExpired(_timeSource);
				ProcessInbound();
			} catch (Exception ex) {
				// This thread has no other backstop: an exception from a subscriber reached via
				// _outbound.Publish would otherwise take down the process. Same containment
				// policy as QueuedHandler / TimeoutService.
				_log.ErrorException(ex, $"Error while processing delayed messages in {nameof(LaterService)}.");
			}

			if (_pending.Count > 0) {//wait for next queued item or inbound msg
				_timeSource.WaitFor(_pending.Keys[0], _processNext);
			} else { //nothing queued, wait for inbound msg
				_processNext.Wait();
			}
			_processNext.Reset();
		}
		_stopped = true;
		// Full fence: the write above must be visible before the read below, or this thread
		// and a concurrently disposing thread could each miss the other's flag and orphan
		// the handle (volatile alone permits the store-load reordering).
		Interlocked.MemoryBarrier();
		// If Dispose gave up joining this thread, disposal of the wait handle was handed
		// off to us: we are provably done touching it here.
		if (_disposeHandleOnExit) {
			DisposeProcessNext();
		}
	}

	private void ProcessInbound() {
		if (_disposed) { return; }
		while (_inbound.TryDequeue(out var msg) && !_stop) {
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
				if (_stop) { return; }
				_outbound.Publish(toDispatch[i]);
			}
		} while (_pending.Count <= 0);
	}

	private bool _disposed;
	private volatile bool _disposeHandleOnExit;
	private bool _handleDisposed; // guarded by _handleLock

	// Disposal of _processNext can race between Dispose and the exiting pump thread;
	// claim ownership so exactly one of them disposes it.
	private void DisposeProcessNext() {
		lock (_handleLock) {
			if (_handleDisposed) { return; }
			_handleDisposed = true;
		}
		_processNext.Dispose();
	}

	public void Dispose() {
		if (_disposed) { return; }
		_disposed = true;
		if (_thread is null) {
			// never started: no pump thread to join, and nothing else can touch our state
			_stop = true;
			_stopped = true;
		} else {
			try { Stop((int)QueuedHandler.DefaultStopWaitTimeout.TotalMilliseconds); } catch {/*don't throw exceptions here, but don't wait forever either */}
		}
		if (_stopped) {
			// The pump thread has exited; safe to tear down the state it owned.
			_pending.Clear();
			while (_inbound.TryDequeue(out _)) { /* empty the queue */ }
			DisposeProcessNext();
		} else {
			// The pump thread outlived the join, and disposing the wait handle under a live
			// waiter is an unhandled ObjectDisposedException on that thread. Hand disposal
			// off to the pump (end of Process), then re-check for the race where it exited
			// before seeing the handoff. The fence pairs with the one in Process.
			_disposeHandleOnExit = true;
			Interlocked.MemoryBarrier();
			if (_stopped) {
				DisposeProcessNext();
			}
		}
	}
}
