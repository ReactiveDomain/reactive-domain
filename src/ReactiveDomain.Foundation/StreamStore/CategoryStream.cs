using ReactiveDomain.Logging;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Util;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation;

/// <summary>
/// One catch-up read over one aggregate's category stream, shared by every read model that needs it.
/// </summary>
/// <remarks>
/// <para>Every <c>StartAsync&lt;TAggregate&gt;()</c> on a <see cref="ReadModelBase"/> is an independent full
/// read of a category — its own reader, its own listener, its own pass over the history. When several read
/// models fold the same category they each open one, and the cost lands on whatever path constructs them.
/// A <see cref="CategoryStream{TAggregate}"/> reads the category <b>once</b> and hands every event to each
/// read model that attached a relay, so N interested models cost one read instead of N.</para>
///
/// <para><b>Why the relay forwards everything.</b> <see cref="RelayTo"/> forwards the whole category, not a
/// chosen set of event types, because that is precisely what a read model sees when it reads the category
/// itself: its bus is handed every event and dispatches only the types it subscribed. Forwarding a subset
/// would quietly change which messages arrive, and would break the "count the streams that went live"
/// idiom consumers use to flush a buffered cache — this way each source contributes exactly one
/// <see cref="StreamStoreMsgs.CatchupSubscriptionBecameLive"/>, the same as one <c>StartAsync</c> did, so
/// those counts stay correct without being touched.</para>
///
/// <para><b>Why it is a relay and not a shared subscription.</b> The target is handed the message through
/// <see cref="ReadModelBase.Handle(IMessage)"/>, which enqueues onto the target's own queue. Subscribing a
/// read model's typed handler directly to another model's event stream would instead run that handler on
/// the <i>source's</i> thread and mutate the target's state outside its own reader lock — a data race, and
/// an easy one to write by accident.</para>
///
/// <para><b>What this does not change:</b> a read model that takes a source rather than opening its own
/// catch-up no longer counts that stream in its own <see cref="ReadModelBase.IsLive"/>, because
/// <c>IsLive</c> is the <c>WhenAll</c> of the read tasks it started itself. A consumer that wants a
/// hydration barrier must await the source as well as the model, or gate on the forwarded go-live.</para>
/// </remarks>
/// <typeparam name="TAggregate">The aggregate whose category stream this reads.</typeparam>
public sealed class CategoryStream<TAggregate> : IDisposable where TAggregate : class, IEventSource {
	private const long NoPosition = -1;

	private static readonly ILogger Log = LogManager.GetLogger("ReactiveDomain");

	private readonly IConfiguredConnection _connection;
	private readonly string _name;
	private readonly object _relayLock = new();
	private readonly List<Relay> _relays = [];

	private IStreamReader? _reader;
	private IListener? _listener;
	private IDisposable? _listenerSubscription;
	// Read and written only under _relayLock, so "attach a relay" and "start" cannot interleave: a
	// relay is either registered before the read begins or refused.
	private bool _started;
	private long _lastRelayedPosition = NoPosition;
	private long _positionAtGoLive = NoPosition;
	private volatile bool _disposed;

	/// <summary>
	/// Creates a shared reader over the category stream of <typeparamref name="TAggregate"/>. Nothing is
	/// read until <see cref="Start"/> or <see cref="StartAsync"/> is called.
	/// </summary>
	/// <param name="connection">A configured connection to the stream store.</param>
	public CategoryStream(IConfiguredConnection connection) {
		Ensure.NotNull(connection, nameof(connection));
		_connection = connection;
		_name = $"{nameof(CategoryStream<TAggregate>)}:{typeof(TAggregate).Name}";
		StreamName = connection.StreamNamer.GenerateForCategory(typeof(TAggregate));
	}

	/// <summary>
	/// The category stream this reads, as the store names it.
	/// </summary>
	/// <remarks>A checkpoint is a stream name and a position, and this type already owns the position
	/// (<see cref="PositionAtGoLive"/>). A consumer merging checkpoints from several sources needs the
	/// key as well, and the stream is the only thing that knows for certain which category it read —
	/// rebuilding the name from the connection's namer is a second derivation that nothing keeps in step
	/// with the first.</remarks>
	public string StreamName { get; }

	/// <summary>
	/// The category position of the last event relayed before this stream forwarded its go-live, or
	/// <c>null</c> if it went live having relayed nothing. This is the checkpoint a relayed read model
	/// persists for this source.
	/// </summary>
	/// <remarks>
	/// <para><b>The stream is the position authority.</b> A relayed <see cref="IMessage"/> carries no
	/// position, and stamping one onto it would change the delivered type and so change which handlers
	/// dispatch. The stream therefore counts positions as it reads and publishes the one value a
	/// subscriber needs.</para>
	/// <para><b>Subscribers checkpoint at the forwarded go-live, and only there.</b> At that moment a
	/// subscriber has, by queue order, consumed exactly everything relayed before the go-live, so this
	/// value is precisely its own position. Mid-live it is not: the subscriber's consumed position lags
	/// this one by whatever is sitting in its queue, so a checkpoint taken at <see cref="Dispose"/> would
	/// overclaim by that queue depth and the next restore would skip events the model never folded. The
	/// only cost of not writing one at dispose is re-folding the live-session delta on the next open.</para>
	/// </remarks>
	public long? PositionAtGoLive {
		get {
			var position = Interlocked.Read(ref _positionAtGoLive);
			return position == NoPosition ? null : position;
		}
	}

	/// <summary>
	/// Forwards every event in this category onto <paramref name="target"/>'s own queue, for as long as
	/// the returned subscription is held. Must be called before <see cref="Start"/>.
	/// </summary>
	/// <param name="target">The read model to relay to.</param>
	/// <param name="fromPosition">
	/// The category position this target has already folded — from a snapshot's checkpoint for this
	/// stream. During the catch-up read the relay drops events at positions at or below it; the go-live
	/// and every live-phase event are always delivered. <c>null</c> (the default) means "I hold nothing",
	/// and the target is relayed the whole category.
	/// </param>
	/// <returns>A subscription; disposing it detaches this relay.</returns>
	/// <exception cref="InvalidOperationException">The stream has already been started, so this relay
	/// would miss the history and the go-live.</exception>
	/// <remarks>
	/// <para>Gating is per relay, so one read serves a restored subscriber and a from-scratch one at the
	/// same time: <see cref="Start"/> reads from the <b>lowest</b> position any relay still needs, and the
	/// relays that already hold more simply skip what they already have. A single subscriber without a
	/// checkpoint therefore forces the full read, which is correct — it needs the whole history — and
	/// costs the restored subscribers nothing but a comparison per event.</para>
	/// <para>Gating never touches the go-live, so a consumer counting one go-live per source still counts
	/// correctly whether it was restored or not.</para>
	/// <para><b>Attaching after <see cref="Start"/> throws</b>, for the same reason a second
	/// <see cref="Start"/> does: the alternative fails silently. A late relay misses the history that has
	/// already been read and the go-live that has already been forwarded, and since exactly one go-live
	/// per source is what subscriber liveness counting rests on, its target would sit forever one go-live
	/// short of live with nothing to indicate why. The rule is: attach every relay, then start.</para>
	/// </remarks>
	public IDisposable RelayTo(ReadModelBase target, long? fromPosition = null) {
		Ensure.NotNull(target, nameof(target));
		if (fromPosition is not null)
			Ensure.Nonnegative(fromPosition.Value, nameof(fromPosition));
		ObjectDisposedException.ThrowIf(_disposed, this);

		lock (_relayLock) {
			if (_started) {
				throw new InvalidOperationException(
					$"{_name} has already been started, so this relay would miss the history already read " +
					"and the go-live already forwarded, and its target would never go live. Attach every " +
					"relay before starting.");
			}

			var relay = new Relay(this, target, fromPosition);
			_relays.Add(relay);
			return relay;
		}
	}

	/// <summary>
	/// Reads the category on the calling thread, relaying as it goes, then starts a listener for live
	/// events. Call once, <b>after</b> every <see cref="RelayTo"/>.
	/// </summary>
	/// <param name="cancelWaitToken">Cancellation token passed to the live listener.</param>
	/// <exception cref="InvalidOperationException">The stream has already been started.</exception>
	/// <remarks><para>Reading does not start in the constructor, and that is the whole point of having
	/// this method. A read model opening its own catch-up subscribes its handlers first and starts reading
	/// last, all inside one constructor, so it cannot miss its own history. A shared reader cannot: its
	/// subscribers attach from <i>their</i> constructors, which necessarily run after this one. Reading on
	/// construction would therefore lose every event delivered before a subscriber attached — and, worse,
	/// the go-live along with them, so a model counting streams to live would never reach its count and
	/// never flush its cache. That failure is a race, and it gets <i>more</i> likely as the store gets
	/// faster: the faster the read, the more reliably it wins and the more data goes missing.</para>
	/// <para>This marks the stream started before it reads, so a <see cref="RelayTo"/> racing it is
	/// refused rather than half-attached: a relay is either registered before the read begins or
	/// throws.</para></remarks>
	public void Start(CancellationToken cancelWaitToken = default) {
		ObjectDisposedException.ThrowIf(_disposed, this);
		lock (_relayLock) {
			if (_started)
				throw new InvalidOperationException($"{_name} has already been started.");
			_started = true;
		}

		var checkpoint = LowestPositionNeeded();
		Log.Debug($"{_name} starting catch-up from checkpoint {(checkpoint?.ToString() ?? "start of stream")} " +
				  $"for {RelayCount()} relay(s).");

		using (var reader = _connection.GetReader(_name, _ => { })) {
			_reader = reader;
			// The reader's position is the event it is handing over, so the gate is evaluated where the
			// position is known. Nothing is queued here that the reader must wait for — a relayed message
			// is enqueued on the target's own queue synchronously — so the read needs no completion check.
			reader.Handle = message => RelayRead(message, reader.Position);
			reader.Read<TAggregate>(() => true, checkpoint);
			checkpoint = reader.Position ?? checkpoint;
			_reader = null;
		}

		if (_disposed)
			return;

		var listener = _connection.GetListener(_name);
		_listener = listener;
		_listenerSubscription = listener.EventStream.SubscribeToAll(new AdHocHandler<IMessage>(RelayLive));
		listener.Start<TAggregate>(checkpoint, false, false, cancelWaitToken);
	}

	/// <summary>
	/// Runs <see cref="Start"/> on a task pool thread. Call once, <b>after</b> every
	/// <see cref="RelayTo"/>; a relay attached after this throws, and <see cref="Start"/> says why.
	/// </summary>
	/// <param name="cancelWaitToken">Cancellation token passed to the live listener.</param>
	/// <returns>A task that completes when the catch-up read has finished and the live listener has been
	/// started. It does not signal that the stream has gone live.</returns>
	/// <exception cref="InvalidOperationException">The stream has already been started. Thrown from the
	/// returned task.</exception>
	public Task StartAsync(CancellationToken cancelWaitToken = default) =>
		Task.Run(() => Start(cancelWaitToken), cancelWaitToken);

	/// <summary>
	/// Stops reading and listening and detaches every relay. Dispose after the read models that relay
	/// from this stream.
	/// </summary>
	public void Dispose() {
		if (_disposed)
			return;
		_disposed = true;
		_reader?.Cancel();
		_listenerSubscription?.Dispose();
		_listener?.Dispose();
		lock (_relayLock) {
			_relays.Clear();
		}
	}

	/// <summary>
	/// The lowest position any relay still needs, as a reader checkpoint: <c>null</c> — read the whole
	/// category — as soon as one relay holds no checkpoint, since it needs the whole history.
	/// </summary>
	private long? LowestPositionNeeded() {
		long? lowest = null;
		lock (_relayLock) {
			foreach (var relay in _relays) {
				if (relay.FromPosition is not { } position)
					return null;
				if (lowest is null || position < lowest)
					lowest = position;
			}
		}

		return lowest;
	}

	private void RelayRead(IMessage message, long? position) {
		if (_disposed)
			return;
		if (position is { } read)
			Interlocked.Exchange(ref _lastRelayedPosition, read);

		foreach (var relay in CurrentRelays()) {
			// Gated relays are the restored ones: they already folded everything up to their checkpoint.
			// An unknown position delivers rather than drops — a duplicate an idempotent fold absorbs is
			// the cheaper mistake of the two.
			if (relay.FromPosition is { } gate && position is { } current && current <= gate)
				continue;
			relay.Target.Handle(message);
		}
	}

	private void RelayLive(IMessage message) {
		if (_disposed)
			return;
		if (message is StreamStoreMsgs.CatchupSubscriptionBecameLive) {
			// Captured before the go-live is forwarded, so a subscriber reading it while handling the
			// go-live sees the position that go-live stands for.
			var position = Interlocked.Read(ref _lastRelayedPosition);
			Interlocked.Exchange(ref _positionAtGoLive, position);
			Log.Debug($"{_name} live at position {(position == NoPosition ? "none" : position.ToString())}.");
		} else if (_listener is { } listener) {
			Interlocked.Exchange(ref _lastRelayedPosition, listener.Position);
		}

		// Live-phase events go to every relay unconditionally: the listener starts where the read stopped,
		// so anything it delivers is past every checkpoint a relay could honestly hold. (A checkpoint from
		// a store that was since rebuilt or rewound is not honest, and no gate can rescue it.)
		foreach (var relay in CurrentRelays()) {
			relay.Target.Handle(message);
		}
	}

	private Relay[] CurrentRelays() {
		lock (_relayLock) {
			return _relays.ToArray();
		}
	}

	private int RelayCount() {
		lock (_relayLock) {
			return _relays.Count;
		}
	}

	private void Detach(Relay relay) {
		lock (_relayLock) {
			_relays.Remove(relay);
		}
	}

	private sealed class Relay(CategoryStream<TAggregate> source, ReadModelBase target, long? fromPosition)
		: IDisposable {
		public ReadModelBase Target { get; } = target;
		public long? FromPosition { get; } = fromPosition;

		private bool _disposed;

		public void Dispose() {
			if (_disposed)
				return;
			_disposed = true;
			source.Detach(this);
		}
	}
}
