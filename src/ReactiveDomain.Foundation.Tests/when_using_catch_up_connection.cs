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

		_catchUp.WaitForCatchUp(TestTimeouts.ThrottleWaitFor, rm);
		Assert.Equal(20, rm.Sum); // deterministic: no IsOrBecomesTrue needed after the barrier

		AppendEvents(10, 5);
		_catchUp.WaitForCatchUp(TestTimeouts.ThrottleWaitFor, rm);
		Assert.Equal(70, rm.Sum);
	}

	[Fact]
	public void timeout_names_the_busy_read_model() {
		AppendEvents(1, 2);
		using var gate = new ManualResetEventSlim(false);
		using var rm = new SumReadModel(_catchUp, gate);
		rm.StartAsync(_stream);
		_catchUp.WaitForCatchUp(TestTimeouts.ThrottleWaitFor, rm);

		// The gated value blocks the model's own queue thread, not delivery: the listener hands the
		// event on and its checkpoint advances, so the deterministic laggard is the busy queue.
		AppendEvents(1, GatedValue);
		var ex = Assert.Throws<TimeoutException>(() =>
			_catchUp.WaitForCatchUp(TimeSpan.FromMilliseconds(200), rm));
		Assert.Contains($"queue {nameof(SumReadModel)}", ex.Message);

		gate.Set();
		_catchUp.WaitForCatchUp(TestTimeouts.ThrottleWaitFor, rm);
		Assert.Equal(2 + GatedValue, rm.Sum);
	}

	[Fact]
	public void disposed_listeners_do_not_pin_the_barrier() {
		AppendEvents(1, 1);
		var listener = _catchUp.GetListener("torn down");
		listener.Start(_stream);
		_catchUp.WaitForCatchUp(TestTimeouts.ThrottleWaitFor);

		// A model deliberately torn down while the connection lives on (snapshot-restore and
		// kill/resume flows) can never deliver again; the barrier must converge without it.
		listener.Dispose();
		AppendEvents(2, 1);

		_catchUp.WaitForCatchUp(TestTimeouts.ThrottleWaitFor);
	}

	[Fact]
	public void the_barrier_delivers_the_went_live_marker() {
		AppendEvents(3, 1);
		using var rm = new SumReadModel(_catchUp);
		rm.StartAsync(_stream);

		_catchUp.WaitForCatchUp(TestTimeouts.ThrottleWaitFor, rm);
		Assert.True(rm.SawWentLive, "The barrier's queue drain includes the ModelWentLive it enqueues.");
	}

	[Fact]
	public void signal_when_live_delivers_the_went_live_marker() {
		AppendEvents(3, 1);
		using var rm = new SumReadModel(_catchUp);
		rm.StartAsync(_stream);
		rm.SignalWhenLive();

		AssertEx.IsOrBecomesTrue(() => rm.SawWentLive, TestTimeouts.ThrottleWaitFor);
		Assert.Equal(3, rm.Sum); // the marker runs behind the history the arming waited for
	}

	[Fact]
	public void queued_listeners_are_not_tracked() {
		AppendEvents(5, 3);
		var queued = _catchUp.GetQueuedListener("queued");
		Assert.IsType<QueuedStreamListener>(queued);
		queued.Start(_stream);

		// the queued listener does not feed the barrier, so no laggard is reported for it
		_catchUp.WaitForCatchUp(TestTimeouts.ThrottleWaitFor);
		queued.Dispose();
	}

	public void Dispose() {
		_conn.Dispose();
	}

	public record CatchUpTestEvent(int Value) : Event;

	private const int GatedValue = 999;

	private sealed class SumReadModel :
		ReadModelBase,
		IHandle<CatchUpTestEvent>,
		IHandle<ModelWentLive> {
		private readonly ManualResetEventSlim? _gate;
		public long Sum { get; private set; }
		public bool SawWentLive { get; private set; }

		public SumReadModel(IConfiguredConnection connection, ManualResetEventSlim? gate = null)
			: base(nameof(SumReadModel), connection) {
			_gate = gate;
			// ReSharper disable RedundantTypeArgumentsOfMethod
			EventStream.Subscribe<CatchUpTestEvent>(this);
			EventStream.Subscribe<ModelWentLive>(this);
			// ReSharper restore RedundantTypeArgumentsOfMethod
		}

		void IHandle<CatchUpTestEvent>.Handle(CatchUpTestEvent @event) {
			if (@event.Value == GatedValue)
				_gate?.Wait();
			Sum += @event.Value;
		}

		void IHandle<ModelWentLive>.Handle(ModelWentLive _) {
			SawWentLive = true;
		}
	}
}
