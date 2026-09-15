using ReactiveDomain.Messaging;

namespace ReactiveDomain.Foundation.Tests;

/// <summary>
/// An <see cref="IConfiguredConnection"/> whose readers call back before and after reading a
/// named stream, giving tests a deterministic hold point inside <c>ReadModelBase.Start</c>'s
/// read-then-subscribe sequence. Everything else passes straight through.
/// </summary>
/// <remarks>
/// <paramref name="afterRead"/> is handed the model's own queue-enqueue delegate, so a test can
/// place a message where the read would have left one: queued, unhandled, and ahead of whatever
/// the start path queues next.
/// </remarks>
internal sealed class HookedConnection(
	IConfiguredConnection inner,
	Action<string>? beforeRead = null,
	Action<string, Action<IMessage>>? afterRead = null) : IConfiguredConnection {
	public IStreamStoreConnection Connection => inner.Connection;
	public IStreamNameBuilder StreamNamer => inner.StreamNamer;
	public IEventSerializer Serializer => inner.Serializer;

	public IListener GetListener(string name) => inner.GetListener(name);
	public IListener GetQueuedListener(string name) => inner.GetQueuedListener(name);

	public IStreamReader GetReader(string name, Action<IMessage> handle) =>
		new HookedReader(inner.GetReader(name, handle), handle, beforeRead, afterRead);

	public IRepository GetRepository(bool caching = false, Func<Guid>? currentPolicyUserId = null) =>
		inner.GetRepository(caching, currentPolicyUserId);

	public ICorrelatedRepository GetCorrelatedRepository(
		IRepository? baseRepository = null, bool caching = false, Func<Guid>? currentPolicyUserId = null) =>
		inner.GetCorrelatedRepository(baseRepository, caching, currentPolicyUserId);

	private sealed class HookedReader(
		IStreamReader inner,
		Action<IMessage> handle,
		Action<string>? beforeRead,
		Action<string, Action<IMessage>>? afterRead) : IStreamReader {
		public long? Position => inner.Position;
		public StreamCheckpoint? Checkpoint => inner.Checkpoint;
		public string StreamName => inner.StreamName;
		public Action<IMessage> Handle { set => inner.Handle = value; }
		public Action<IMessage, StreamCheckpoint?> PairedHandle { set => inner.PairedHandle = value; }

		public bool Read(string stream, Func<bool> completionCheck, long? checkpoint = null, long? count = null,
			bool readBackwards = false) {
			beforeRead?.Invoke(stream);
			var read = inner.Read(stream, completionCheck, checkpoint, count, readBackwards);
			afterRead?.Invoke(stream, handle);
			return read;
		}

		public bool Read(Type tMessage, Func<bool> completionCheck, long? checkpoint = null, long? count = null,
			bool readBackwards = false) => inner.Read(tMessage, completionCheck, checkpoint, count, readBackwards);

		public bool Read<TAggregate>(Guid id, Func<bool> completionCheck, long? checkpoint = null, long? count = null,
			bool readBackwards = false) where TAggregate : class, IEventSource =>
			inner.Read<TAggregate>(id, completionCheck, checkpoint, count, readBackwards);

		public bool Read<TAggregate>(Func<bool> completionCheck, long? checkpoint = null, long? count = null,
			bool readBackwards = false) where TAggregate : class, IEventSource =>
			inner.Read<TAggregate>(completionCheck, checkpoint, count, readBackwards);

		public void Cancel() => inner.Cancel();
		public void Dispose() => inner.Dispose();
	}
}
