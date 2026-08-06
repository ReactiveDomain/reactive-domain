using System.Diagnostics.CodeAnalysis;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

/// <summary>
/// A relay-fed model is handed events from a stream it never listens to, and its snapshot still has
/// to carry that stream's position — the feeding side has nowhere else to resume from.
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class when_using_external_snapshot_positions : IClassFixture<StreamStoreConnectionFixture> {
	private readonly IConfiguredConnection _configuredConnection;
	private readonly IStreamStoreConnection _conn;
	private readonly IEventSerializer _serializer = new JsonMessageSerializer();
	private readonly IStreamNameBuilder _namer =
		new PrefixedCamelCaseStreamNameBuilder(nameof(when_using_external_snapshot_positions));

	private readonly string _ownStream;
	private readonly Guid _ownAggId;
	private readonly string _relayedStream;

	private const int OwnEventValue = 2;
	private const int RelayedEventValue = 100;

	public when_using_external_snapshot_positions(StreamStoreConnectionFixture fixture) {
		_conn = fixture.Connection;
		_conn.Connect();
		_configuredConnection = new ConfiguredConnection(_conn, _namer, _serializer);

		_ownAggId = Guid.NewGuid();
		_ownStream = _namer.GenerateForAggregate(typeof(ExternalPositionTestAggregate), _ownAggId);
		_relayedStream = _namer.GenerateForAggregate(typeof(ExternalPositionTestAggregate), Guid.NewGuid());

		AppendEvents(10, _ownStream, OwnEventValue);
		AppendEvents(5, _relayedStream, RelayedEventValue);
	}

	private void AppendEvents(int count, string streamName, int value) {
		for (var i = 0; i < count; i++) {
			_conn.AppendToStream(streamName, ExpectedVersion.Any, null,
				_serializer.Serialize(new ExternalPositionTestEvent(i, value)));
		}
	}

	private ReadModelState StateWith(List<StreamCheckpoint>? externalCheckpoints) =>
		new(
			nameof(RelayFedReadModel),
			[new StreamCheckpoint(_ownStream, 9)],
			new RelayFedReadModel.MyState { Count = 10, Sum = 20 },
			externalCheckpoints);

	[Fact]
	public void external_positions_round_trip_untouched() {
		var snapshot = StateWith([new StreamCheckpoint(_relayedStream, 4)]);

		using var rm = new RelayFedReadModel(_configuredConnection, _relayedStream, snapshot);
		var roundTripped = rm.GetState();

		Assert.NotNull(roundTripped.ExternalCheckpoints);
		var external = Assert.Single(roundTripped.ExternalCheckpoints);
		Assert.Equal(_relayedStream, external.StreamName);
		Assert.Equal(4, external.Version);
		// The model's own checkpoints are unaffected by the external entry.
		Assert.NotNull(roundTripped.Checkpoints);
		Assert.Equal(_ownStream, Assert.Single(roundTripped.Checkpoints).StreamName);
	}

	[Fact]
	public void a_state_with_no_external_positions_is_unchanged() {
		// The three-argument shape every existing state was written with.
		var snapshot = new ReadModelState(
			nameof(RelayFedReadModel),
			[new StreamCheckpoint(_ownStream, 9)],
			new RelayFedReadModel.MyState { Count = 10, Sum = 20 });
		Assert.Null(snapshot.ExternalCheckpoints);

		using var rm = new RelayFedReadModel(_configuredConnection, _relayedStream, snapshot);
		AssertEx.IsOrBecomesTrue(() => rm.Count == 10, TestTimeouts.ThrottleWaitFor);

		// Nothing was recorded, so nothing is emitted: the same shape goes back out.
		Assert.Null(rm.GetState().ExternalCheckpoints);
		Assert.False(rm.HasExternalCheckpoint(_relayedStream, out _));
	}

	[Fact]
	public void a_model_reads_its_external_positions_while_restoring() {
		var snapshot = StateWith([new StreamCheckpoint(_relayedStream, 4)]);

		using var rm = new RelayFedReadModel(_configuredConnection, _relayedStream, snapshot);

		// Recorded before ApplyState ran: restore is when a model has to tell its relay where
		// to resume from.
		Assert.Equal(4, rm.PositionSeenWhileRestoring);
	}

	[Fact]
	public void an_unrecorded_external_stream_reports_no_position() {
		using var rm = new RelayFedReadModel(
			_configuredConnection, _relayedStream, StateWith([new StreamCheckpoint(_relayedStream, 4)]));

		Assert.False(rm.HasExternalCheckpoint("no.such.stream", out var checkpoint));
		Assert.Null(checkpoint);
	}

	[Fact]
	public void external_positions_never_start_a_listener() {
		var snapshot = StateWith([new StreamCheckpoint(_relayedStream, 1)]);

		using var rm = new RelayFedReadModel(_configuredConnection, _relayedStream, snapshot);
		AssertEx.IsOrBecomesTrue(() => rm.Count == 10, TestTimeouts.ThrottleWaitFor);

		// Append to the relayed stream first, then to the model's own stream. The own listener
		// is running, so once its event lands, anything the relayed stream would have delivered
		// has had at least as long to arrive.
		AppendEvents(1, _relayedStream, RelayedEventValue);
		AppendEvents(1, _ownStream, OwnEventValue);
		AssertEx.IsOrBecomesTrue(() => rm.Count == 11, TestTimeouts.ThrottleWaitFor);

		Assert.Equal(11, rm.Count);
		Assert.Equal(20 + OwnEventValue, rm.Sum); // no relayed event was folded in
		Assert.Equal(_ownStream, Assert.Single(rm.GetCheckpoint()).StreamName);
	}

	// The version resumes the stream; the $all position is what makes this checkpoint comparable
	// against one taken on a different stream. A snapshot has to carry both or the second is lost.
	[Fact]
	public void a_relayed_all_position_is_carried_into_the_next_snapshot() {
		var snapshot = StateWith([new StreamCheckpoint(_relayedStream, 1, new Position(512, 512))]);

		using var rm = new RelayFedReadModel(_configuredConnection, _relayedStream, snapshot);
		Assert.True(rm.HasExternalCheckpoint(_relayedStream, out var restored));
		Assert.Equal(new Position(512, 512), restored.Position);

		rm.Relay(new ExternalPositionTestEvent(2, RelayedEventValue), 2, new Position(1024, 1024));

		var saved = Assert.Single(rm.GetState().ExternalCheckpoints!);
		Assert.Equal(2, saved.Version);
		Assert.Equal(new Position(1024, 1024), saved.Position);
	}

	// A source that cannot supply an $all position still checkpoints its version.
	[Fact]
	public void a_relayed_position_is_optional() {
		var snapshot = StateWith([new StreamCheckpoint(_relayedStream, 1)]);

		using var rm = new RelayFedReadModel(_configuredConnection, _relayedStream, snapshot);
		rm.Relay(new ExternalPositionTestEvent(2, RelayedEventValue), 2);

		var saved = Assert.Single(rm.GetState().ExternalCheckpoints!);
		Assert.Equal(2, saved.Version);
		Assert.Null(saved.Position);
	}

	[Fact]
	public void a_relayed_position_is_carried_into_the_next_snapshot() {
		var snapshot = StateWith([new StreamCheckpoint(_relayedStream, 1)]);

		using var rm = new RelayFedReadModel(_configuredConnection, _relayedStream, snapshot);
		AssertEx.IsOrBecomesTrue(() => rm.Count == 10, TestTimeouts.ThrottleWaitFor);

		// What a relay does: hand over the event, then the position it came from.
		rm.Relay(new ExternalPositionTestEvent(2, RelayedEventValue), 2);
		rm.Relay(new ExternalPositionTestEvent(3, RelayedEventValue), 3);

		var saved = rm.GetState();
		Assert.NotNull(saved.ExternalCheckpoints);
		Assert.Equal(3, Assert.Single(saved.ExternalCheckpoints).Version);

		using var restored = new RelayFedReadModel(_configuredConnection, _relayedStream, saved);
		Assert.Equal(3, restored.PositionSeenWhileRestoring);
		AssertEx.IsOrBecomesTrue(() => restored.Count == 12, TestTimeouts.ThrottleWaitFor);
	}

	public sealed class RelayFedReadModel :
		SnapshotReadModel,
		IHandle<ExternalPositionTestEvent> {
		private readonly string _relayedStream;

		public RelayFedReadModel(
			IConfiguredConnection configuredConnection,
			string relayedStream,
			ReadModelState snapshot) :
			base(nameof(RelayFedReadModel), configuredConnection) {
			_relayedStream = relayedStream;
			// ReSharper disable once RedundantTypeArgumentsOfMethod
			EventStream.Subscribe<ExternalPositionTestEvent>(this);
			Restore(snapshot);
		}

		public long Sum { get; private set; }
		public long Count { get; private set; }

		/// <summary>The relayed stream's position the model found while restoring, or -1.</summary>
		public long PositionSeenWhileRestoring { get; private set; } = -1;

		/// <summary>Stands in for a relay: apply the event, then record where it came from.</summary>
		public void Relay(ExternalPositionTestEvent @event, long version, Position? position = null) {
			DirectApply(@event);
			SetExternalCheckpoint(_relayedStream, version, position);
		}

		/// <summary>Exposes the protected accessor so the tests can assert on it.</summary>
		public bool HasExternalCheckpoint(
			string streamName,
			[NotNullWhen(true)] out StreamCheckpoint? checkpoint) =>
			TryGetExternalCheckpoint(streamName, out checkpoint);

		void IHandle<ExternalPositionTestEvent>.Handle(ExternalPositionTestEvent @event) {
			Sum += @event.Value;
			Count++;
		}

		protected override void ApplyState(ReadModelState snapshot) {
			if (snapshot.State is not MyState state) {
				throw new ArgumentException(
					$"Unknown state object: Expected {nameof(MyState)}, got {snapshot.State?.GetType().Name}");
			}

			Count = state.Count;
			Sum = state.Sum;
			if (TryGetExternalCheckpoint(_relayedStream, out var checkpoint)) {
				PositionSeenWhileRestoring = checkpoint.Version;
			}
		}

		public override ReadModelState GetState() =>
			new(
				nameof(RelayFedReadModel),
				GetCheckpoint(),
				new MyState { Sum = Sum, Count = Count },
				GetExternalCheckpoints());

		public class MyState {
			public long Sum { get; set; }
			public long Count { get; set; }
		}
	}

	public class ExternalPositionTestAggregate : EventDrivenStateMachine;

	public record ExternalPositionTestEvent(int Number, int Value) : IMessage {
		public Guid MsgId { get; private set; } = Guid.NewGuid();
		public readonly int Number = Number;
		public readonly int Value = Value;
	}
}
