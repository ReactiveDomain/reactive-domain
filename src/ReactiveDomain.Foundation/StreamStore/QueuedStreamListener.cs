using System.Collections.Concurrent;
using System.Reactive;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Util;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation;

public class QueuedStreamListener : StreamListener, IHandle<IMessage> {
	protected readonly QueuedHandler SyncQueue;
	private readonly ManualResetEventSlim _isLive = new(false);
	private long _pendingCount;
	private readonly ManualResetEventSlim _running = new(true);

	public QueuedStreamListener(
		string name,
		IStreamStoreConnection connection,
		IStreamNameBuilder streamNameBuilder,
		IEventSerializer serializer,
		string? busName = null,
		Action<Unit>? liveProcessingStarted = null,
		Action<SubscriptionDropReason, Exception?>? subscriptionDropped = null) :
		base(name, connection, streamNameBuilder, serializer, busName, liveProcessingStarted, subscriptionDropped) {
		SyncQueue = new QueuedHandler(this, "SyncListenerQueue");
	}

	// One entry per event off the store, in stream order, carrying the clocks the message itself
	// cannot until #211 gives it an envelope. This listener has a queue of its own between the store
	// and the model, so recording at GotEvent would checkpoint events that have not left this
	// listener yet; the entries wait here until the queue thread reaches them.
	private readonly ConcurrentQueue<(RecordedEvent Event, bool Published)> _delivering = new();

	protected override void GotEvent(RecordedEvent recordedEvent) {
		if (_disposed)
			return; //todo: fix dispose
		var @event = Serializer.Deserialize(recordedEvent) as IMessage;
		// Enqueued before the publish: the queue thread must never dequeue a message whose clocks
		// have not arrived. An event that deserializes to nothing publishable still gets an entry —
		// it is real history, and dropping it would leave a hole the checkpoint has to skip over.
		_delivering.Enqueue((recordedEvent, @event is not null));
		if (@event is not null)
			SyncQueue.Publish(@event);
	}
	public void Handle(IMessage @event) {
		_running.Wait();
		// Taken here rather than in GotEvent: this listener's own queue sits between the store and the
		// subscriber, so this thread is where it delivers, and this is the publish a holder has to be
		// able to exclude.
		lock (DeliveryLock) {
			//todo: this needs to take a RecordedEvent
			Bus.Publish(@event);
			// After the publish, so the checkpoint follows the model's queue rather than leading it.
			// Unpublishable events ahead of this one are recorded on the way past: nothing waits behind
			// them. A trailing run of them holds the checkpoint back until the next message, which costs
			// a replay of events that deserialize to nothing anyway.
			while (_delivering.TryDequeue(out var delivered)) {
				RecordDelivered(delivered.Event);
				if (delivered.Published)
					break;
			}
		}

		if (!_isLive.IsSet) {
			Interlocked.Decrement(ref _pendingCount);
			if (IsLive && (Interlocked.Read(ref _pendingCount) <= 0 || SyncQueue.Idle)) {
				_isLive.Set();
			}
		}
	}

	public override void Start(string streamName, long? checkpoint = null, bool waitUntilLive = false, bool validateStream = false, CancellationToken cancelWaitToken = default) {
		_isLive.Reset();

		SyncQueue.Start();
		base.Start(streamName, checkpoint, waitUntilLive, validateStream, cancelWaitToken);

		Interlocked.Exchange(ref _pendingCount, SyncQueue.MessageCount);
		if (Interlocked.Read(ref _pendingCount) <= 0 || SyncQueue.Idle) {
			_isLive.Set();
		}

		if (waitUntilLive) {
			_isLive.Wait(cancelWaitToken);
		}
	}

	private IDisposable Pause() {
		_running.Reset();
		return new Disposer(() => { Resume(); return Unit.Default; });
	}

	private void Resume() {
		_running.Set();
	}
	private bool _disposed;
	protected override void Dispose(bool disposing) {
		if (!_disposed) {
			if (disposing) {
				_isLive.Set();
				_running.Set(); // release any in-flight Handle so Stop can join the queue thread
				SyncQueue.Stop();
				_running.Dispose();
			}
			_disposed = true;
		}
		base.Dispose(disposing);
	}
}
