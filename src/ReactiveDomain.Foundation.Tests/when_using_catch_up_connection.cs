using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using ReactiveDomain.Testing.EventStore;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

// ReSharper disable once InconsistentNaming
public sealed class when_using_catch_up_connection : IDisposable {
	private static readonly JsonMessageSerializer Serializer = new();
	private static readonly PrefixedCamelCaseStreamNameBuilder Namer =
		new(nameof(when_using_catch_up_connection));

	private readonly MockStreamStoreConnection _conn = new(nameof(when_using_catch_up_connection));
	private readonly CatchUpConnection _catchUp;
	private readonly string _stream = $"catchUpTest{Guid.NewGuid():N}";

	public when_using_catch_up_connection() {
		_conn.Connect();
		_catchUp = new CatchUpConnection(new ConfiguredConnection(_conn, Namer, Serializer));
	}

	private void AppendEvents(int count, int value) {
		for (var i = 0; i < count; i++) {
			_conn.AppendToStream(_stream, ExpectedVersion.Any, null, Serializer.Serialize(new CatchUpTestEvent(value)));
		}
	}

	[Fact]
	public void wait_for_catch_up_returns_when_everything_is_delivered_and_applied() {
		AppendEvents(10, 2);
		using var rm = new SumReadModel(_catchUp);
		rm.StartAsync(_stream);

		_catchUp.WaitForCatchUp(TestTimeouts.WaitFor, rm);
		Assert.Equal(20, rm.Sum); // deterministic: no IsOrBecomesTrue needed after the barrier

		AppendEvents(10, 5);
		_catchUp.WaitForCatchUp(TestTimeouts.WaitFor, rm);
		Assert.Equal(70, rm.Sum);
	}

	[Fact]
	public void timeout_names_the_lagging_stream() {
		AppendEvents(1, 1);
		var listener = _catchUp.GetListener("laggard");
		listener.Start(_stream);
		_catchUp.WaitForCatchUp(TestTimeouts.WaitFor);

		// a dead listener must read as a laggard, not as caught up
		listener.Dispose();
		AppendEvents(2, 1);

		var ex = Assert.Throws<TimeoutException>(() => _catchUp.WaitForCatchUp(TimeSpan.FromMilliseconds(200)));
		Assert.Contains(_stream, ex.Message);
		Assert.Contains("delivered 0 of 2", ex.Message);
	}

	[Fact]
	public void queued_listeners_are_not_tracked() {
		AppendEvents(5, 3);
		var queued = _catchUp.GetQueuedListener("queued");
		Assert.IsType<QueuedStreamListener>(queued);
		queued.Start(_stream);

		// the queued listener does not feed the barrier, so no laggard is reported for it
		_catchUp.WaitForCatchUp(TestTimeouts.WaitFor);
		queued.Dispose();
	}

	public void Dispose() {
		_conn.Dispose();
	}

	public record CatchUpTestEvent(int Value) : Event;

	private sealed class SumReadModel :
		ReadModelBase,
		IHandle<CatchUpTestEvent> {
		public long Sum { get; private set; }

		public SumReadModel(IConfiguredConnection connection)
			: base(nameof(SumReadModel), connection) {
			// ReSharper disable once RedundantTypeArgumentsOfMethod
			EventStream.Subscribe<CatchUpTestEvent>(this);
		}

		void IHandle<CatchUpTestEvent>.Handle(CatchUpTestEvent @event) {
			Sum += @event.Value;
		}
	}
}
