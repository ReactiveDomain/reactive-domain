using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using ReactiveDomain.Testing.EventStore;
using Xunit;

namespace ReactiveDomain.Foundation.Tests.StreamListenerTests;

// The live transition reaches subscribers the way an event does and is counted the way one is, so it
// has to be held the way one is. Published outside the delivery lock it could land in a subscriber's
// queue while a capture believed delivery was stopped — and no checkpoint names it, so a model that
// handles it would hold state its own checkpoints disown.
// ReSharper disable once InconsistentNaming
public sealed class when_a_listener_goes_live_while_delivery_is_held : IDisposable {
	private readonly MockStreamStoreConnection _conn;
	private readonly StreamListener _listener;
	// Empty, so the only thing the listener has to publish on starting is the transition itself.
	private readonly string _stream = $"liveHoldTest-{Guid.NewGuid():N}";
	private readonly ManualResetEventSlim _live = new(false);

	public when_a_listener_goes_live_while_delivery_is_held() {
		_conn = new MockStreamStoreConnection(nameof(when_a_listener_goes_live_while_delivery_is_held));
		_conn.Connect();
		_listener = new StreamListener(nameof(when_a_listener_goes_live_while_delivery_is_held),
			_conn, new PrefixedCamelCaseStreamNameBuilder(), new JsonMessageSerializer());
		_listener.EventStream.Subscribe(
			new AdHocHandler<StreamStoreMsgs.CatchupSubscriptionBecameLive>(_ => _live.Set()));
	}

	public void Dispose() {
		_listener.Dispose();
		_conn.Dispose();
	}

	[Fact]
	public async Task the_transition_waits_for_the_hold_that_an_event_would_wait_for() {
		var entered = new ManualResetEventSlim(false);
		var hold = _listener.HoldDelivery();
		Task starting;
		try {
			starting = Task.Run(() => {
				entered.Set();
				_listener.Start(_stream, null, false);
			});
			Assert.True(entered.Wait(TestTimeouts.ThrottleWaitFor), "the start never began");

			// An absence, so it is given long enough to have happened: starting a listener on an empty
			// stream against an in-memory store is otherwise immediate. Spun rather than awaited
			// because a hold has to be released by the thread that took it.
			Assert.False(SpinWait.SpinUntil(() => starting.IsCompleted, TimeSpan.FromMilliseconds(500)),
				"the listener started through a hold on its delivery");
			Assert.False(_live.IsSet, "the live transition was published while delivery was held");
		} finally {
			hold.Dispose();
		}

		await starting.WaitAsync(TestTimeouts.ThrottleWaitFor);
		Assert.True(_live.Wait(TestTimeouts.ThrottleWaitFor), "the live transition never arrived");
	}
}
