using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

/// <summary>
/// Covers <see cref="SnapshotReadModel.SaveIfFarEnough"/> and <see cref="SnapshotReadModel.SaveOnDispose"/>.
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class when_saving_snapshots_by_distance : IClassFixture<StreamStoreConnectionFixture>, IDisposable {
	private readonly IStreamStoreConnection _conn;
	private readonly IConfiguredConnection _configured;
	private readonly IEventSerializer _serializer = new JsonMessageSerializer();
	private readonly IStreamNameBuilder _namer =
		new PrefixedCamelCaseStreamNameBuilder(nameof(when_saving_snapshots_by_distance));
	private readonly List<IDisposable> _disposables = [];

	public when_saving_snapshots_by_distance(StreamStoreConnectionFixture fixture) {
		_conn = fixture.Connection;
		_conn.Connect();
		_configured = new ConfiguredConnection(_conn, _namer, _serializer);
	}

	private string StreamFor(Guid id) => _namer.GenerateForAggregate(typeof(TestAggregate), id);

	private void Append(string stream, int count) {
		for (var i = 0; i < count; i++) {
			_conn.AppendToStream(stream, ExpectedVersion.Any, null, _serializer.Serialize(new DistanceTestEvent(1)));
		}
	}

	private T Track<T>(T disposable) where T : IDisposable {
		_disposables.Add(disposable);
		return disposable;
	}

	[Fact]
	public async Task a_distance_gate_skips_until_enough_events_have_been_applied() {
		var id = Guid.NewGuid();
		var stream = StreamFor(id);
		Append(stream, 3);
		var saved = new List<ReadModelState>();
		var rm = Track(new DistanceSnapshotModel(_configured, id));
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		Assert.False(await rm.TrySave(10, saved));
		Assert.Empty(saved);

		Assert.True(await rm.TrySave(1, saved));
		var first = Assert.Single(saved);
		Assert.Equal(2, first.Checkpoints![0].Version);

		Append(stream, 1);
		AssertEx.IsOrBecomesTrue(() => rm.Count == 4, TestTimeouts.ThrottleWaitFor);
		Assert.False(await rm.TrySave(5, saved));
		Assert.True(await rm.TrySave(1, saved));
		Assert.Equal(2, saved.Count);
		Assert.Equal(3, saved[1].Checkpoints![0].Version);
	}

	[Fact]
	public async Task dispose_writes_when_idle_and_respects_the_gate() {
		var id = Guid.NewGuid();
		var stream = StreamFor(id);
		Append(stream, 4);
		var saved = new List<ReadModelState>();
		var rm = Track(new DistanceSnapshotModel(_configured, id));
		rm.AutoSaveOnDispose(saved, minDistance: 2);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);
		AssertEx.IsOrBecomesTrue(() => rm.Idle, TestTimeouts.ThrottleWaitFor);

		rm.Dispose();

		var snap = Assert.Single(saved);
		Assert.Equal(3, snap.Checkpoints![0].Version);
		Assert.Equal(4, ((DistanceSnapshotModel.State)snap.State).Count);
	}

	[Fact]
	public async Task dispose_drains_a_parked_handler_then_saves_applied_state() {
		var id = Guid.NewGuid();
		var stream = StreamFor(id);
		Append(stream, 3);
		var saved = new List<ReadModelState>();
		using var park = new ManualResetEventSlim(false);
		using var parked = new ManualResetEventSlim(false);
		var rm = Track(new DistanceSnapshotModel(_configured, id, park, parked, parkAt: 3));
		rm.AutoSaveOnDispose(saved, minDistance: 0);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);
		Assert.Equal(3, rm.Count);

		Append(stream, 2);
		Assert.True(parked.Wait(TestTimeouts.ThrottleWaitFor));

		park.Set();
		rm.Dispose();

		var snap = Assert.Single(saved);
		Assert.Equal(5, ((DistanceSnapshotModel.State)snap.State).Count);
		Assert.Equal(4, snap.Checkpoints![0].Version);
	}

	public void Dispose() => _disposables.ForEach(d => d.Dispose());

	private sealed class DistanceSnapshotModel : SnapshotReadModel, IHandle<DistanceTestEvent> {
		private readonly ManualResetEventSlim? _park;
		private readonly ManualResetEventSlim? _parked;
		private readonly int _parkAt;

		public DistanceSnapshotModel(
			IConfiguredConnection connection,
			Guid id,
			ManualResetEventSlim? park = null,
			ManualResetEventSlim? parked = null,
			int parkAt = int.MaxValue)
			: base(nameof(DistanceSnapshotModel), connection) {
			_park = park;
			_parked = parked;
			_parkAt = parkAt;
			EventStream.Subscribe<DistanceTestEvent>(this);
			Start<TestAggregate>(id);
		}

		public int Count { get; private set; }

		void IHandle<DistanceTestEvent>.Handle(DistanceTestEvent @event) {
			if (_park is not null && Count == _parkAt) {
				_parked!.Set();
				_park.Wait();
			}
			Count++;
		}

		public Task<bool> TrySave(int minDistance, List<ReadModelState> into) =>
			SaveIfFarEnough(minDistance, (state, _) => {
				into.Add(state);
				return Task.CompletedTask;
			});

		public void AutoSaveOnDispose(List<ReadModelState> into, int minDistance) =>
			SaveOnDispose(into.Add, minDistance);

		protected override void ApplyState(ReadModelState snapshot) =>
			Count = ((State)snapshot.State).Count;

		public override ReadModelState GetState() =>
			new(nameof(DistanceSnapshotModel), GetCheckpoint(), new State { Count = Count });

		public sealed class State {
			public int Count { get; set; }
		}
	}

	public record DistanceTestEvent(int Value) : Event;
}
