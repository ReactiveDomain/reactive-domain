using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using ReactiveDomain.Testing.EventStore;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

/// <summary>
/// Covers reconnect on drop (#267): the listener resumes from its last version, and a reconnect
/// that cannot be made faults <see cref="ReadModelBase.Subscriptions"/>.
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class when_a_live_subscription_drops : IDisposable {
	private readonly MockStreamStoreConnection _conn;
	private readonly IConfiguredConnection _configured;
	private readonly IEventSerializer _serializer = new JsonMessageSerializer();
	private readonly IStreamNameBuilder _namer =
		new PrefixedCamelCaseStreamNameBuilder(nameof(when_a_live_subscription_drops));
	private readonly List<IDisposable> _disposables = [];

	public when_a_live_subscription_drops() {
		_conn = new MockStreamStoreConnection(nameof(when_a_live_subscription_drops));
		_conn.Connect();
		_configured = new ConfiguredConnection(_conn, _namer, _serializer);
	}

	private string NewStream() => _namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());

	private void Append(string stream, int count) {
		for (var i = 0; i < count; i++) {
			_conn.AppendToStream(stream, ExpectedVersion.Any, null, _serializer.Serialize(new DropTestEvent(i)));
		}
	}

	private T Track<T>(T disposable) where T : IDisposable {
		_disposables.Add(disposable);
		return disposable;
	}

	[Fact]
	public async Task reconnects_from_the_last_delivered_version_and_does_not_redeliver() {
		var stream = NewStream();
		Append(stream, 3);
		var rm = Track(new DropTestReadModel(_configured));
		rm.Start(stream);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);
		Assert.Equal(3, rm.Count);

		_conn.DropSubscriptions();
		Append(stream, 2);

		AssertEx.IsOrBecomesTrue(() => rm.Count == 5, TestTimeouts.ThrottleWaitFor);
		Assert.Equal(5, rm.Count); // nothing from 0..2 was folded again
		Assert.False(rm.Subscriptions.IsFaulted);
	}

	[Fact]
	public async Task a_failed_reconnect_faults_subscriptions() {
		var stream = NewStream();
		var rm = Track(new DropTestReadModel(_configured));
		_conn.RemainingSuccessfulSubscribes = 1; // the Start
		rm.Start(stream);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		_conn.DropSubscriptions();

		var thrown = await Assert.ThrowsAsync<SubscriptionDroppedException>(
			() => rm.Subscriptions.WaitAsync(TestTimeouts.ThrottleWaitFor));
		Assert.Equal(stream, thrown.StreamName);
		Assert.Equal(SubscriptionDropReason.ConnectionClosed, thrown.Reason);
	}

	[Fact]
	public async Task an_immediate_drop_on_reconnect_faults_without_nesting() {
		var stream = NewStream();
		var rm = Track(new DropTestReadModel(_configured));
		rm.Start(stream);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		_conn.ImmediateDropsOnSubscribe = 10;
		_conn.DropSubscriptions();

		var thrown = await Assert.ThrowsAsync<SubscriptionDroppedException>(
			() => rm.Subscriptions.WaitAsync(TestTimeouts.ThrottleWaitFor));
		Assert.Equal(stream, thrown.StreamName);
	}

	[Fact]
	public void queued_reconnect_does_not_redeliver_events_already_in_the_queue() {
		var stream = NewStream();
		Append(stream, 3);
		using var park = new ManualResetEventSlim(false);
		using var parked = new ManualResetEventSlim(false);
		var handled = 0;
		var listener = Track(new QueuedStreamListener(
			nameof(queued_reconnect_does_not_redeliver_events_already_in_the_queue),
			_conn, _namer, _serializer));
		listener.EventStream.Subscribe(new AdHocHandler<DropTestEvent>(_ => {
			if (Volatile.Read(ref handled) == 0) {
				parked.Set();
				park.Wait();
			}
			Interlocked.Increment(ref handled);
		}));
		listener.Start(stream);
		Assert.True(parked.Wait(TestTimeouts.ThrottleWaitFor));

		Append(stream, 2);
		_conn.DropSubscriptions();
		Append(stream, 2);
		park.Set();

		AssertEx.IsOrBecomesTrue(() => handled == 7, TestTimeouts.ThrottleWaitFor);
		Assert.Equal(7, handled);
	}

	public void Dispose() {
		_disposables.ForEach(d => d.Dispose());
		_conn.Dispose();
	}

	private sealed class DropTestReadModel : ReadModelBase, IHandle<DropTestEvent> {
		public DropTestReadModel(IConfiguredConnection connection) : base(nameof(DropTestReadModel), connection) {
			EventStream.Subscribe<DropTestEvent>(this);
		}

		public int Count { get; private set; }

		void IHandle<DropTestEvent>.Handle(DropTestEvent @event) => Count++;
	}

	public record DropTestEvent(int Number) : Event;
}
