using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using ReactiveDomain.Testing.EventStore;
using Xunit;

namespace ReactiveDomain.Foundation.Tests.StreamListenerTests;

// A listener publishes an event and then records it, and a hold exists to make sure nobody samples
// between the two — where a subscriber already has an event the checkpoint does not name. Driving a
// model's handler apart from its listener does not open that window; only stopping inside the publish
// does, which is what these do.
// ReSharper disable once InconsistentNaming
public sealed class when_holding_delivery_mid_flight : IDisposable {
	private readonly MockStreamStoreConnection _conn;
	private readonly JsonMessageSerializer _serializer = new();
	private readonly string _stream = $"midFlightTest-{Guid.NewGuid():N}";
	private readonly ManualResetEventSlim _publishing = new(false);
	private readonly ManualResetEventSlim _release = new(false);
	private readonly List<IListener> _listeners = [];

	public when_holding_delivery_mid_flight() {
		_conn = new MockStreamStoreConnection(nameof(when_holding_delivery_mid_flight));
		_conn.Connect();
	}

	public void Dispose() {
		_release.Set();
		_listeners.ForEach(l => l.Dispose());
		_conn.Dispose();
	}

	public record MidFlightEvent : Event;

	private T Started<T>(T listener) where T : IListener {
		_listeners.Add(listener);
		// Stops the delivery inside the publish, so the event is in the subscriber's hands while the
		// listener has not yet recorded it.
		listener.EventStream.Subscribe(new AdHocHandler<MidFlightEvent>(_ => {
			_publishing.Set();
			_release.Wait();
		}));
		listener.Start(_stream, null, false);
		return listener;
	}

	private void Append() =>
		_conn.AppendToStream(_stream, ExpectedVersion.Any, null, _serializer.Serialize(new MidFlightEvent()));

	private async Task AssertHoldWaitsForTheDelivery(IListener listener) {
		// Off-thread: the store dispatches an append on the calling thread, so appending here would
		// block this test inside the very handler it is trying to observe.
		var appending = Task.Run(Append);
		Assert.True(_publishing.Wait(TestTimeouts.ThrottleWaitFor), "the delivery never reached a subscriber");

		var acquired = new ManualResetEventSlim(false);
		var holding = Task.Run(() => {
			using var hold = listener.HoldDelivery();
			acquired.Set();
		});

		// An absence, given long enough to have happened. A hold taken here would sample a checkpoint
		// that disowns an event a subscriber already has.
		Assert.False(acquired.Wait(TimeSpan.FromMilliseconds(500)),
			"delivery was held while an event sat between its publish and its record");

		_release.Set();
		await holding.WaitAsync(TestTimeouts.ThrottleWaitFor);
		await appending.WaitAsync(TestTimeouts.ThrottleWaitFor);
		Assert.True(acquired.IsSet);
		Assert.Equal(0, listener.Checkpoint?.Version);
	}

	[Fact]
	public Task a_listener_holds_only_once_the_delivery_it_started_is_recorded() =>
		AssertHoldWaitsForTheDelivery(Started(new StreamListener(
			nameof(when_holding_delivery_mid_flight), _conn,
			new PrefixedCamelCaseStreamNameBuilder(), _serializer)));

	// The queued listener delivers from its own queue thread, so its publish and its record are a
	// different pair of statements from the base listener's, and need holding on their own account.
	[Fact]
	public Task a_queued_listener_holds_only_once_the_delivery_it_started_is_recorded() =>
		AssertHoldWaitsForTheDelivery(Started(new QueuedStreamListener(
			nameof(when_holding_delivery_mid_flight), _conn,
			new PrefixedCamelCaseStreamNameBuilder(), _serializer)));
}
