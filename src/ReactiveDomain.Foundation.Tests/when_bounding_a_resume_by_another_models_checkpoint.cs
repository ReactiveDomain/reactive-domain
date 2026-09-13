using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

/// <summary>
/// Covers <see cref="StreamCheckpoint.BoundedBy(StreamCheckpoint?)"/>: a model hydrated from another
/// model's state resumes from whichever of the two was further behind, so nothing the source's state
/// may lack is skipped.
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class when_bounding_a_resume_by_another_models_checkpoint : IClassFixture<StreamStoreConnectionFixture>, IDisposable {
	private readonly IStreamStoreConnection _conn;
	private readonly IConfiguredConnection _configured;
	private readonly IEventSerializer _serializer = new JsonMessageSerializer();
	private readonly IStreamNameBuilder _namer =
		new PrefixedCamelCaseStreamNameBuilder(nameof(when_bounding_a_resume_by_another_models_checkpoint));
	private readonly List<IDisposable> _disposables = [];

	public when_bounding_a_resume_by_another_models_checkpoint(StreamStoreConnectionFixture fixture) {
		_conn = fixture.Connection;
		_conn.Connect();
		_configured = new ConfiguredConnection(_conn, _namer, _serializer);
	}

	private static StreamCheckpoint At(string stream, long? version, Position? position = null) => new(stream, version, position);

	[Fact]
	public void a_source_that_is_behind_holds_the_resume_back_to_it() {
		var bounded = At("s", 9).BoundedBy(At("s", 4, new Position(400, 400)));

		Assert.Equal(4, bounded.Version);
		Assert.Equal(new Position(400, 400), bounded.Position); // the position belongs to the event resumed after
	}

	[Fact]
	public void a_source_that_is_ahead_or_level_leaves_the_resume_alone() {
		var own = At("s", 9);

		Assert.Same(own, own.BoundedBy(At("s", 12)));
		Assert.Same(own, own.BoundedBy(At("s", 9)));
	}

	[Fact]
	public void a_source_with_nothing_recorded_means_from_the_beginning() {
		// Not position zero: how much of its state exists cannot be established.
		Assert.Null(At("s", 9).BoundedBy(null).Version);
		Assert.Null(At("s", 9).BoundedBy(At("s", null)).Version);
		Assert.Null(At("s", null).BoundedBy(At("s", 3)).Version);
	}

	[Fact]
	public void a_checkpoint_on_another_stream_is_refused() {
		var thrown = Assert.Throws<ArgumentException>(() => At("s", 9).BoundedBy(At("t", 4)));

		Assert.Contains("same stream", thrown.Message);
	}

	[Fact]
	public void over_a_set_only_the_streams_the_source_reads_are_bounded() {
		List<StreamCheckpoint> own = [At("shared", 9), At("shared-empty-at-source", 5), At("mine-alone", 7)];
		List<StreamCheckpoint> source = [At("shared", 4), At("shared-empty-at-source", null), At("source-alone", 2)];

		var bounded = StreamCheckpoint.BoundedBy(own, source).ToDictionary(c => c.StreamName, c => c.Version);

		Assert.Equal(4, bounded["shared"]);
		Assert.Null(bounded["shared-empty-at-source"]);
		Assert.Equal(7, bounded["mine-alone"]); // nothing hydrated from the source depends on it
		Assert.DoesNotContain("source-alone", bounded.Keys);
	}

	/// <summary>
	/// The failure the bound exists for, end to end. Model B stores a sum it hydrates from model A's
	/// state. A stopped at version 2, B at version 5. Hydrated from A and resumed from its own 5, B is
	/// short events 3..5 forever; resumed from the bound, it is whole.
	/// </summary>
	[Fact]
	public async Task hydrating_from_a_source_that_was_behind_and_resuming_from_the_bound_is_whole() {
		var stream = _namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());
		for (var i = 0; i < 6; i++) {
			_conn.AppendToStream(stream, ExpectedVersion.Any, null, _serializer.Serialize(new BoundTestEvent(10)));
		}
		var sourceSnapshot = new ReadModelState("A", [At(stream, 2)], 30L);   // A folded 0..2
		var ownCheckpoints = new List<StreamCheckpoint> { At(stream, 5) };     // B had folded 0..5

		var naive = Track(new HydratedSumReadModel(_configured,
			new ReadModelState("B", ownCheckpoints, sourceSnapshot.State)));
		var bounded = Track(new HydratedSumReadModel(_configured,
			new ReadModelState("B", StreamCheckpoint.BoundedBy(ownCheckpoints, sourceSnapshot.Checkpoints!), sourceSnapshot.State)));
		await Task.WhenAll(
			naive.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor),
			bounded.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor));

		Assert.Equal(30, naive.Sum);   // hydrated short, and events 3..5 sit before its resume point
		Assert.Equal(60, bounded.Sum); // 3..5 redelivered; nothing double-counted
	}

	private T Track<T>(T disposable) where T : IDisposable {
		_disposables.Add(disposable);
		return disposable;
	}

	public void Dispose() {
		_disposables.ForEach(d => d.Dispose());
	}

	private sealed class HydratedSumReadModel : SnapshotReadModel, IHandle<BoundTestEvent> {
		public HydratedSumReadModel(IConfiguredConnection connection, ReadModelState hydratedFromSource)
			: base(nameof(HydratedSumReadModel), connection) {
			// ReSharper disable once RedundantTypeArgumentsOfMethod
			EventStream.Subscribe<BoundTestEvent>(this);
			Restore(hydratedFromSource);
		}

		public long Sum { get; private set; }

		public void Handle(BoundTestEvent @event) => Sum += @event.Value;

		protected override void ApplyState(ReadModelState snapshot) => Sum = (long)snapshot.State;

		public override ReadModelState GetState() => new(nameof(HydratedSumReadModel), GetCheckpoint(), Sum);
	}

	public record BoundTestEvent(int Value) : Event;
}
