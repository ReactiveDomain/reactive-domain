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
	// does not. This listener has a queue of its own between the store and the model, so recording
	// at GotEvent would checkpoint events that have not left this listener yet; the entries wait
	// here until the queue thread reaches them.
	private readonly ConcurrentQueue<(RecordedEvent Event, bool Published)> _delivering = new();
	private long _received = -1;
	private int _receivedAny;

	protected override long? ReconnectFrom() =>
		Volatile.Read(ref _receivedAny) != 0 ? Interlocked.Read(ref _received) : base.ReconnectFrom();

	protected override bool ReconnectFromUnderDeliveryLock => false;

	protected override void GotEvent(RecordedEvent recordedEvent) {
		if (Stopped)
			return;
		var @event = TryDeserialize(recordedEvent);
		// Enqueued before the publish: the queue thread must never dequeue a message whose clocks
		// have not arrived. An event that deserializes to nothing publishable still gets an entry —
		// it is real history, and dropping it would leave a hole the checkpoint has to skip over.
		// Not under DeliveryLock: Handle holds that lock for the subscriber, and reconnect catch-up
		// runs GotEvent on another thread.
		_delivering.Enqueue((recordedEvent, @event is not null));
		Interlocked.Exchange(ref _received, recordedEvent.EventNumber);
		Interlocked.Exchange(ref _receivedAny, 1);
		if (@event is not null)
			SyncQueue.Publish(@event);
	}

	protected override void PublishLive() {
		// Through this queue, after events already accepted: the subscribe thread must not take
		// DeliveryLock — Handle holds it for the subscriber.
		if (Stopped)
			return;
		SyncQueue.Publish(new StreamStoreMsgs.CatchupSubscriptionBecameLive());
	}

	public void Handle(IMessage @event) {
		_running.Wait();
		// Taken here rather than in GotEvent: this listener's own queue sits between the store and the
		// subscriber, so this thread is where it delivers, and this is the publish a holder has to be
		// able to exclude.
		lock (DeliveryLock) {
			if (@event is StreamStoreMsgs.CatchupSubscriptionBecameLive) {
				Deliver(@event, Checkpoint);
			} else {
				// Unpublishable events ahead of this one are recorded on the way past: nothing waits behind
				// them. A trailing run of them holds the checkpoint back until the next message, which costs
				// a replay of events that deserialize to nothing anyway.
				RecordedEvent? mine = null;
				while (_delivering.TryDequeue(out var delivered)) {
					if (delivered.Published) {
						mine = delivered.Event;
						break;
					}
					RecordDelivered(delivered.Event);
				}
				Deliver(@event, mine is null ? null : CheckpointOf(mine));
				// After the publish, so the checkpoint follows the model's queue rather than leading it.
				if (mine is not null)
					RecordDelivered(mine);
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
	protected override void Dispose(bool disposing) {
		if (disposing) {
			// Source first, then drain, then the queue: with the subscription gone, nothing new
			// arrives, and Stop without a drain would abandon what was already handed to us.
			StopListening();
			_isLive.Set();
			_running.Set();
			SpinWait.SpinUntil(() => SyncQueue.Idle, QueuedHandler.DefaultStopWaitTimeout);
			SyncQueue.Stop();
			_running.Dispose();
		}
		base.Dispose(disposing);
	}
}
