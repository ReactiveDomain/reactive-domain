using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing.EventStore;

namespace ReactiveDomain.Testing;

/// <summary>
/// Opens an arrange region in $all: written to the marker stream on construction and after every
/// <see cref="MockRepositorySpecification.ClearQueues"/>. A plain <see cref="Message"/>, not an
/// <see cref="Event"/>, so RepositoryEvents never captures it.
/// </summary>
public sealed record SetupStartMarker : Message;

/// <summary>
/// Delivery fence marker appended by <see cref="MockRepositorySpecification.AwaitEventDelivery"/>.
/// A plain <see cref="Message"/>, not an <see cref="Event"/>, so RepositoryEvents never captures
/// it — but its arrival still completes a WaitForMsgId wait.
/// </summary>
public sealed record DeliveryFence : Message;

public class MockRepositorySpecification : DispatcherSpecification {
	protected readonly IRepository MockRepository;
	public IRepository Repository => MockRepository;
	public readonly TestQueue RepositoryEvents;
	public IStreamNameBuilder StreamNameBuilder { get; }
	public IStreamStoreConnection StreamStoreConnection { get; }
	public IEventSerializer EventSerializer { get; }
	public IConfiguredConnection ConfiguredConnection { get; }

	public string Schema { get; }

	/// <summary>
	/// Creates a mock repository.
	/// </summary>
	/// <param name="schema">Schema prefix for stream name.</param>
	/// <param name="dataStore">Stream store connection.</param>
	private MockRepositorySpecification(string schema, IStreamStoreConnection dataStore) {
		Schema = schema;
		StreamNameBuilder = string.IsNullOrEmpty(schema) ? new PrefixedCamelCaseStreamNameBuilder() : new PrefixedCamelCaseStreamNameBuilder(schema);
		StreamStoreConnection = dataStore;
		StreamStoreConnection.Connect();
		EventSerializer = new JsonMessageSerializer();
		MockRepository = new StreamStoreRepository(StreamNameBuilder, StreamStoreConnection, EventSerializer);

		ConfiguredConnection = new ConfiguredConnection(StreamStoreConnection, StreamNameBuilder, EventSerializer);

		var connectorBus = new InMemoryBus("connector");
		StreamStoreConnection.SubscribeToAll(evt => {
			if (evt is ProjectedEvent) { return; }
			var msg = (IMessage?)EventSerializer.Deserialize(evt);
			if (msg is not null)
				connectorBus.Publish(msg);
		});
		RepositoryEvents = new TestQueue(connectorBus, [typeof(Event)]);
		WriteMarker(new SetupStartMarker()); // bracket the first arrange region
	}

	// A '$'-free, '-'-free stream: no $ce category link is emitted for markers, and the $et link
	// that is emitted resolves to a ProjectedEvent the connector drops.
	private const string MarkerStream = "setupMarkers";

	/// <summary>
	/// Delivery fence: blocks until every event committed before this call has been delivered to
	/// <see cref="RepositoryEvents"/>. Appends a <see cref="DeliveryFence"/> marker and waits for
	/// its id; because $all delivers in order, the fence's arrival proves every earlier commit is
	/// already queued. The marker never appears in <see cref="RepositoryEvents"/>, so it cannot
	/// break a later AssertEmpty. Covers events committed before the call (synchronous handler
	/// saves), not async cascades — those need a read-model catch-up barrier.
	/// </summary>
	/// <remarks>
	/// Against the synchronous mock this is a near-no-op: delivery has already happened when the
	/// fence is called. Adopt fence-then-assert now; the calls become load-bearing when the
	/// backing store delivers asynchronously.
	/// </remarks>
	public void AwaitEventDelivery() {
		var fence = new DeliveryFence();
		WriteMarker(fence);
		RepositoryEvents.WaitForMsgId(fence.MsgId, TestTimeouts.WaitFor);
	}

	private void WriteMarker(Message marker) =>
		StreamStoreConnection.AppendToStream(
			MarkerStream, ExpectedVersion.Any, credentials: null, EventSerializer.Serialize(marker));

	/// <summary>
	/// Creates a mock repository with a prefix.
	/// </summary>
	/// <param name="schema">Schema prefix for stream name. Default value is "Test".</param>
	public MockRepositorySpecification(string schema = "Test") : this(schema, new MockStreamStoreConnection(schema)) { }

	/// <summary>
	/// Creates a mock repository connected to a StreamStore. 
	/// </summary>
	/// <param name="dataStore">Stream store connection.</param>
	public MockRepositorySpecification(IStreamStoreConnection dataStore) : this(dataStore.ConnectionName, dataStore) { }

	public override void ClearQueues() {
		AwaitEventDelivery(); // fence async $all delivery so no in-flight event lands post-clear
		RepositoryEvents.Clear();
		base.ClearQueues();
		WriteMarker(new SetupStartMarker()); // open the next arrange region
	}

	public IListener GetListener(string name) =>
		new QueuedStreamListener(
			name,
			StreamStoreConnection,
			StreamNameBuilder,
			EventSerializer);

	private bool _disposed;
	protected override void Dispose(bool disposing) {
		if (_disposed)
			return;
		if (disposing) {
			StreamStoreConnection.Dispose();
			RepositoryEvents.Dispose();
		}
		_disposed = true;
		base.Dispose(disposing);
	}
}
