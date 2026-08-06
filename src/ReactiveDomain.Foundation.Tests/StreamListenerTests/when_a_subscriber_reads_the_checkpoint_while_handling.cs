using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using ReactiveDomain.Testing.EventStore;
using Xunit;

namespace ReactiveDomain.Foundation.Tests.StreamListenerTests;

// A listener publishes an event and only then records it. That ordering is invisible to a capture,
// which holds delivery and so never sees between the two — which makes it look like it does not
// matter. It matters to everyone reading the checkpoint the ordinary way, and this is where it shows:
// a subscriber handling an event must not find the checkpoint already claiming that event, or a
// snapshot taken from a handler would name an event whose own handling had not finished.
// ReSharper disable once InconsistentNaming
public sealed class when_a_subscriber_reads_the_checkpoint_while_handling : IDisposable {
	private readonly MockStreamStoreConnection _conn;
	private readonly JsonMessageSerializer _serializer = new();
	private readonly string _stream = $"checkpointOrderTest-{Guid.NewGuid():N}";
	private readonly StreamListener _listener;

	// What the listener reported as its own position while it was handing each event over, in order.
	private readonly List<long?> _seenWhileHandling = [];

	public when_a_subscriber_reads_the_checkpoint_while_handling() {
		_conn = new MockStreamStoreConnection(nameof(when_a_subscriber_reads_the_checkpoint_while_handling));
		_conn.Connect();
		for (var i = 0; i < 4; i++) {
			_conn.AppendToStream(_stream, ExpectedVersion.Any, null, _serializer.Serialize(new OrderTestEvent()));
		}

		_listener = new StreamListener(nameof(when_a_subscriber_reads_the_checkpoint_while_handling),
			_conn, new PrefixedCamelCaseStreamNameBuilder(), _serializer);
		// Subscribed directly rather than through a queue, so this runs inside the publish and can see
		// what a checkpoint reader would see at that instant.
		_listener.EventStream.Subscribe(new AdHocHandler<OrderTestEvent>(
			_ => _seenWhileHandling.Add(_listener.Checkpoint?.Version)));
		_listener.Start(_stream, null, true);
		AssertEx.IsOrBecomesTrue(() => _seenWhileHandling.Count == 4, TestTimeouts.ThrottleWaitFor);
	}

	public void Dispose() {
		_listener.Dispose();
		_conn.Dispose();
	}

	[Fact]
	public void the_checkpoint_never_names_the_event_being_handled() {
		// Handling event n, the checkpoint is still at n-1 — and at nothing at all for the first,
		// which is the whole reason a version of 0 cannot stand in for "none".
		Assert.Equal([null, 0L, 1L, 2L], _seenWhileHandling);
	}

	[Fact]
	public void the_checkpoint_names_every_event_once_handing_over_is_done() {
		Assert.Equal(3, _listener.Checkpoint?.Version);
	}

	public record OrderTestEvent : Event;
}
