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

	protected override void GotEvent(RecordedEvent recordedEvent) {
		if (_disposed)
			return; //todo: fix dispose
		Interlocked.Exchange(ref StreamPosition, recordedEvent.EventNumber);
		if (Serializer.Deserialize(recordedEvent) is IMessage @event) {
			//todo: this needs to publish a RecordedEvent
			SyncQueue.Publish(@event);
		}
	}
	public void Handle(IMessage @event) {
		_running.Wait();
		//todo: this needs to take a RecordedEvent
		Bus.Publish(@event);

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
				_running.Reset();
				SyncQueue.Stop();
				_running.Dispose();
			}
			_disposed = true;
		}
		base.Dispose(disposing);
	}
}
