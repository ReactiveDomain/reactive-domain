using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

/// <summary>
/// Covers the checkpoint <see cref="IRepository.Save"/> returns: the stream and version the write
/// left behind, the store's position when it reports one, and its use as a wait target against
/// <see cref="ReadModelBase.GetCheckpoint"/>.
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class when_saving_reports_the_write_position : IClassFixture<StreamStoreConnectionFixture>, IDisposable {
	private readonly IStreamStoreConnection _conn;
	private readonly IConfiguredConnection _configured;
	private readonly IStreamNameBuilder _namer =
		new PrefixedCamelCaseStreamNameBuilder(nameof(when_saving_reports_the_write_position));
	private readonly IRepository _repo;
	private readonly List<IDisposable> _disposables = [];

	public when_saving_reports_the_write_position(StreamStoreConnectionFixture fixture) {
		_conn = fixture.Connection;
		_conn.Connect();
		_configured = new ConfiguredConnection(_conn, _namer, new JsonMessageSerializer());
		_repo = _configured.GetRepository();
	}

	private string StreamOf(IEventSource aggregate) => _namer.GenerateForAggregate(aggregate.GetType(), aggregate.Id);

	[Fact]
	public void reports_the_stream_and_version_the_write_left_behind() {
		var agg = new TestWoftamAggregate(Guid.NewGuid());
		agg.ProduceEvents(4);

		var checkpoint = _repo.Save(agg);

		Assert.Equal(StreamOf(agg), checkpoint.StreamName);
		Assert.Equal(4, checkpoint.Version);
		Assert.Equal(((IEventSource)agg).ExpectedVersion, checkpoint.Version);
	}

	[Fact]
	public void each_save_reports_its_own_write() {
		var agg = new TestWoftamAggregate(Guid.NewGuid());
		agg.ProduceEvents(4);
		var first = _repo.Save(agg);
		agg.ProduceEvents(2);
		var second = _repo.Save(agg);

		Assert.Equal(4, first.Version);
		Assert.Equal(6, second.Version);
		Assert.Equal(CheckpointOrder.Before, StreamCheckpoint.Compare([first], [second]));
	}

	[Fact]
	public void position_is_the_one_the_store_recorded_for_the_last_event_written() {
		var agg = new TestWoftamAggregate(Guid.NewGuid());
		agg.ProduceEvents(3);

		var checkpoint = _repo.Save(agg);

		var tail = _conn.ReadStreamBackward(StreamOf(agg), -1, 1);
		Assert.NotNull(tail);
		var last = Assert.Single(tail.Events);
		Assert.Equal(checkpoint.Version, last.EventNumber);
		Assert.NotNull(last.Position);
		Assert.Equal(last.Position, checkpoint.Position);
	}

	[Fact]
	public void nothing_recorded_reports_the_current_version_with_no_position() {
		var agg = new TestWoftamAggregate(Guid.NewGuid());
		agg.ProduceEvents(2);
		_repo.Save(agg);

		var checkpoint = _repo.Save(agg);

		Assert.Equal(StreamOf(agg), checkpoint.StreamName);
		Assert.Equal(2, checkpoint.Version);
		Assert.Null(checkpoint.Position);
	}

	[Fact]
	public void nothing_recorded_on_a_never_written_stream_reports_a_null_version() {
		var agg = new Blank(Guid.NewGuid());

		var checkpoint = _repo.Save(agg);

		Assert.Equal(StreamOf(agg), checkpoint.StreamName);
		Assert.Null(checkpoint.Version);
		Assert.Null(checkpoint.Position);
	}

	[Fact]
	public async Task the_checkpoint_is_a_wait_target_against_a_read_model() {
		var agg = new TestWoftamAggregate(Guid.NewGuid());
		agg.ProduceEvents(4);
		var first = _repo.Save(agg);

		var rm = Track(new CountingReadModel(_configured));
		rm.StartAsync<TestWoftamAggregate>(agg.Id);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		Assert.Equal(CheckpointOrder.Equal, StreamCheckpoint.Compare(rm.GetCheckpoint(), [first]));

		agg.ProduceEvents(3);
		var second = _repo.Save(agg);

		// The model has been delivered the write exactly when its checkpoint is no longer behind it.
		// Delivered, not applied: the handlers run a step behind, so the count follows rather than leads.
		AssertEx.IsOrBecomesTrue(
			() => StreamCheckpoint.Compare(rm.GetCheckpoint(), [second]) is CheckpointOrder.Equal or CheckpointOrder.After,
			TestTimeouts.ThrottleWaitFor);
		AssertEx.IsOrBecomesTrue(() => rm.Count == 7, TestTimeouts.ThrottleWaitFor);
	}

	private T Track<T>(T disposable) where T : IDisposable {
		_disposables.Add(disposable);
		return disposable;
	}

	public void Dispose() {
		_disposables.ForEach(d => d.Dispose());
	}

	private sealed class Blank : EventDrivenStateMachine {
		public Blank(Guid id) { Id = id; }
	}

	private sealed class CountingReadModel : ReadModelBase, IHandle<WoftamEvent> {
		public CountingReadModel(IConfiguredConnection connection) : base(nameof(CountingReadModel), connection) {
			// ReSharper disable once RedundantTypeArgumentsOfMethod
			EventStream.Subscribe<WoftamEvent>(this);
		}

		public int Count { get; private set; }

		public void Handle(WoftamEvent @event) => Count++;
	}
}
