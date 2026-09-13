using ReactiveDomain.Messaging;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests.StreamListenerTests;

/// <summary>
/// Covers <see cref="IListener.SubscribeToDelivery"/>: every message arrives with the checkpoint that
/// names it, on both listeners.
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class when_subscribing_to_paired_delivery : IClassFixture<StreamStoreConnectionFixture>, IDisposable {
	private readonly IStreamStoreConnection _conn;
	private readonly IConfiguredConnection _configured;
	private readonly IEventSerializer _serializer = new JsonMessageSerializer();
	private readonly IStreamNameBuilder _namer =
		new PrefixedCamelCaseStreamNameBuilder(nameof(when_subscribing_to_paired_delivery));
	private readonly List<IDisposable> _disposables = [];

	public when_subscribing_to_paired_delivery(StreamStoreConnectionFixture fixture) {
		_conn = fixture.Connection;
		_conn.Connect();
		_configured = new ConfiguredConnection(_conn, _namer, _serializer);
	}

	private string NewStream() => _namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());

	private void Append(string stream, int count) {
		for (var i = 0; i < count; i++) {
			_conn.AppendToStream(stream, ExpectedVersion.Any, null, _serializer.Serialize(new PairedTestEvent(i)));
		}
	}

	private T Track<T>(T disposable) where T : IDisposable {
		_disposables.Add(disposable);
		return disposable;
	}

	public static TheoryData<bool> Listeners => new() { false, true };

	[Theory]
	[MemberData(nameof(Listeners))]
	public void each_event_arrives_with_the_checkpoint_that_names_it(bool queued) {
		var stream = NewStream();
		Append(stream, 3);
		var listener = Track(queued ? _configured.GetQueuedListener("paired") : _configured.GetListener("paired"));
		var delivered = new List<(IMessage Message, StreamCheckpoint? Checkpoint)>();
		Track(listener.SubscribeToDelivery((m, c) => { lock (delivered) { delivered.Add((m, c)); } }));

		listener.Start(stream);
		Append(stream, 2); // live as well as historical

		AssertEx.IsOrBecomesTrue(() => { lock (delivered) { return delivered.Count(d => d.Message is PairedTestEvent) == 5; } }, TestTimeouts.ThrottleWaitFor);
		lock (delivered) {
			var events = delivered.Where(d => d.Message is PairedTestEvent).ToList();
			Assert.Equal([0, 1, 2, 3, 4], events.Select(d => d.Checkpoint!.Version));
			Assert.All(events, d => Assert.Equal(stream, d.Checkpoint!.StreamName));
			Assert.All(events, d => Assert.NotNull(d.Checkpoint!.Position));
			// Each pairing is the listener's own reading once that event is delivered.
			Assert.Equal(listener.Checkpoint, events[^1].Checkpoint);
		}
	}

	[Fact]
	public void the_live_transition_carries_where_the_listener_stands() {
		var stream = NewStream();
		Append(stream, 2);
		var listener = Track(_configured.GetListener("paired"));
		StreamCheckpoint? atLive = null;
		var wentLive = new ManualResetEventSlim(false);
		Track(listener.SubscribeToDelivery((m, c) => {
			if (m is not StreamStoreMsgs.CatchupSubscriptionBecameLive) { return; }
			atLive = c;
			wentLive.Set();
		}));

		listener.Start(stream);

		Assert.True(wentLive.Wait(TestTimeouts.ThrottleWaitFor));
		Assert.NotNull(atLive);
		Assert.Equal(stream, atLive.StreamName);
		Assert.Equal(1, atLive.Version);
	}

	[Fact]
	public void disposing_the_subscription_stops_the_pairing() {
		var stream = NewStream();
		var listener = Track(_configured.GetListener("paired"));
		var count = 0;
		var subscription = listener.SubscribeToDelivery((_, _) => Interlocked.Increment(ref count));
		listener.Start(stream);
		Append(stream, 1);
		AssertEx.IsOrBecomesTrue(() => Volatile.Read(ref count) >= 1, TestTimeouts.ThrottleWaitFor);

		subscription.Dispose();
		var before = Volatile.Read(ref count);
		Append(stream, 3);
		AssertEx.IsOrBecomesTrue(() => listener.Checkpoint?.Version == 3, TestTimeouts.ThrottleWaitFor);

		Assert.Equal(before, Volatile.Read(ref count));
	}

	public void Dispose() {
		_disposables.ForEach(d => d.Dispose());
	}

	public record PairedTestEvent(int Number) : Event;
}
