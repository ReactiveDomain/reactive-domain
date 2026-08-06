using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using ReactiveDomain.Testing.EventStore;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

// Capture is covered against the listener a ConfiguredConnection hands out and against one stream at
// a time. These are the paths that were not: the queued listener, which is what MockRepositorySpecification
// gives a read model and which has a queue of its own between the store and the model; captures that
// overlap; a model with nothing to capture; and the window where a model is being disposed but does
// not yet say so.
// ReSharper disable once InconsistentNaming
public sealed class when_capturing_off_the_beaten_path : IDisposable {
	private readonly MockStreamStoreConnection _conn;
	private readonly IEventSerializer _serializer = new JsonMessageSerializer();
	private readonly IStreamNameBuilder _namer =
		new PrefixedCamelCaseStreamNameBuilder(nameof(when_capturing_off_the_beaten_path));
	private readonly IConfiguredConnection _configured;

	public when_capturing_off_the_beaten_path() {
		_conn = new MockStreamStoreConnection(nameof(when_capturing_off_the_beaten_path));
		_conn.Connect();
		_configured = new ConfiguredConnection(_conn, _namer, _serializer);
	}

	public void Dispose() => _conn.Dispose();

	private string NewStream() => _namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());

	private void Append(string stream, int count) {
		for (var i = 0; i < count; i++) {
			_conn.AppendToStream(stream, ExpectedVersion.Any, null, _serializer.Serialize(new CountedEvent()));
		}
	}

	public record CountedEvent : Event;

	/// <summary>Hands out the queued listener where a read model asks for a listener.</summary>
	private sealed class QueuedListenerConnection(IConfiguredConnection inner) : IConfiguredConnection {
		public IStreamStoreConnection Connection => inner.Connection;
		public IStreamNameBuilder StreamNamer => inner.StreamNamer;
		public IEventSerializer Serializer => inner.Serializer;
		public IListener GetListener(string name) => inner.GetQueuedListener(name);
		public IListener GetQueuedListener(string name) => inner.GetQueuedListener(name);
		public IStreamReader GetReader(string name, Action<IMessage> handle) => inner.GetReader(name, handle);
		public IRepository GetRepository(bool caching = false, Func<Guid>? currentPolicyUserId = null) =>
			inner.GetRepository(caching, currentPolicyUserId);
		public ICorrelatedRepository GetCorrelatedRepository(
			IRepository? baseRepository = null, bool caching = false, Func<Guid>? currentPolicyUserId = null) =>
			inner.GetCorrelatedRepository(baseRepository, caching, currentPolicyUserId);
	}

	private sealed class CountingModel : SnapshotReadModel, IHandle<CountedEvent> {
		private readonly ManualResetEventSlim _gate = new(true);
		private readonly ManualResetEventSlim _disposing = new(false);
		private readonly ManualResetEventSlim _finishDisposing = new(true);

		public CountingModel(IConfiguredConnection c, ReadModelState? restore = null)
			: base(nameof(CountingModel), c) {
			EventStream.Subscribe<CountedEvent>(this);
			if (restore is not null) { Restore(restore); }
		}

		public long Applied { get; private set; }
		public void Wedge() => _gate.Reset();
		public void Unwedge() => _gate.Set();

		/// <summary>Stops inside Dispose after the base has torn the pump down but before it is marked
		/// disposed, which is the window a capture can be registered into.</summary>
		public void HoldOpenDispose() => _finishDisposing.Reset();
		public bool WaitForDisposeWindow() => _disposing.Wait(TestTimeouts.ThrottleWaitFor);
		public void LetDisposeFinish() => _finishDisposing.Set();

		void IHandle<CountedEvent>.Handle(CountedEvent e) {
			_gate.Wait();
			Applied++;
		}

		public void SeeExternal(string stream, long version) => SetExternalCheckpoint(stream, version);

		protected override void ApplyState(ReadModelState snapshot) => Applied = (long)snapshot.State;
		public override ReadModelState GetState() =>
			new(nameof(CountingModel), GetCheckpoint(), Applied, GetExternalCheckpoints());

		protected override void Dispose(bool disposing) {
			_gate.Set();
			_disposing.Set();
			_finishDisposing.Wait();
			base.Dispose(disposing);
		}
	}

	private static void AssertSelfConsistent(ReadModelState snapshot) =>
		Assert.Equal(snapshot.Checkpoints!.Sum(c => (c.Version ?? -1) + 1), (long)snapshot.State);

	[Fact]
	public async Task a_queued_listener_captures_at_a_cut_like_any_other() {
		var stream = NewStream();
		Append(stream, 5);
		using var rm = new CountingModel(new QueuedListenerConnection(_configured));
		rm.Start(stream, null, true);
		await rm.IsLive;

		// The queued listener holds events in a queue of its own, so drive delivery and application
		// apart the same way and check the cut still lands between them.
		rm.Wedge();
		Append(stream, 6);
		AssertEx.IsOrBecomesTrue(
			() => rm.GetCheckpoint().FirstOrDefault()?.Version == 10, TestTimeouts.ThrottleWaitFor);
		Assert.Equal(5, rm.Applied);

		var capturing = rm.CaptureConsistentState();
		rm.Unwedge();
		var snapshot = await capturing;

		Assert.Equal(11, (long)snapshot.State);
		AssertSelfConsistent(snapshot);
	}

	[Fact]
	public async Task a_queued_listener_restores_what_it_captured() {
		var stream = NewStream();
		Append(stream, 4);
		var queued = new QueuedListenerConnection(_configured);
		using var rm = new CountingModel(queued);
		rm.Start(stream, null, true);
		await rm.IsLive;
		var snapshot = await rm.CaptureConsistentState();

		Append(stream, 3);

		using var restored = new CountingModel(queued, snapshot);
		AssertEx.IsOrBecomesTrue(() => restored.Applied == 7, TestTimeouts.ThrottleWaitFor,
			$"restored {restored.Applied} of 7 appended");
	}

	[Fact]
	public async Task captures_that_overlap_each_describe_their_own_cut() {
		var stream = NewStream();
		Append(stream, 3);
		using var rm = new CountingModel(_configured);
		rm.Start(stream, null, true);
		await rm.IsLive;

		rm.Wedge();
		Append(stream, 2);
		AssertEx.IsOrBecomesTrue(
			() => rm.GetCheckpoint().FirstOrDefault()?.Version == 4, TestTimeouts.ThrottleWaitFor);

		var first = rm.CaptureConsistentState();
		Append(stream, 2);
		AssertEx.IsOrBecomesTrue(
			() => rm.GetCheckpoint().FirstOrDefault()?.Version == 6, TestTimeouts.ThrottleWaitFor);
		var second = rm.CaptureConsistentState();

		rm.Unwedge();
		var earlier = await first;
		var later = await second;

		AssertSelfConsistent(earlier);
		AssertSelfConsistent(later);
		Assert.Equal(5, (long)earlier.State);
		Assert.Equal(7, (long)later.State);
		Assert.Equal(CheckpointOrder.Before, earlier.Compare(later));
	}

	[Fact]
	public async Task a_model_with_nothing_started_captures_an_empty_cut() {
		using var rm = new CountingModel(_configured);
		await rm.IsLive; // vacuously live

		var snapshot = await rm.CaptureConsistentState();

		Assert.Empty(snapshot.Checkpoints!);
		Assert.Equal(0L, (long)snapshot.State);
	}

	[Fact]
	public async Task recording_where_relayed_input_came_from_makes_a_model_capturable_again() {
		var stream = NewStream();
		Append(stream, 2);
		using var rm = new CountingModel(_configured);
		rm.Start(stream, null, true);
		await rm.IsLive;

		rm.DirectApply(new CountedEvent());
		Assert.True(rm.HasUnreplayableInput);

		rm.SeeExternal("relayed-stream", 0);

		Assert.False(rm.HasUnreplayableInput);
		var snapshot = await rm.CaptureConsistentState();
		Assert.Equal("relayed-stream", Assert.Single(snapshot.ExternalCheckpoints!).StreamName);
	}

	// A model that rebuilds its state through DirectApply is doing what Restore asks of it, and the
	// checkpoints restored beside that state describe it exactly — so it is snapshottable, and was not.
	private sealed class RebuildingModel : SnapshotReadModel, IHandle<CountedEvent> {
		public RebuildingModel(IConfiguredConnection c, ReadModelState restore)
			: base(nameof(RebuildingModel), c) {
			EventStream.Subscribe<CountedEvent>(this);
			Restore(restore);
		}
		public long Applied { get; private set; }
		void IHandle<CountedEvent>.Handle(CountedEvent e) => Applied++;

		protected override void ApplyState(ReadModelState snapshot) {
			for (var i = 0; i < (long)snapshot.State; i++) { DirectApply(new CountedEvent()); }
		}
		public override ReadModelState GetState() =>
			new(nameof(RebuildingModel), GetCheckpoint(), Applied, GetExternalCheckpoints());
	}

	[Fact]
	public async Task a_model_that_rebuilds_its_state_while_restoring_can_still_be_captured() {
		var stream = NewStream();
		Append(stream, 4);
		using var built = new CountingModel(_configured);
		built.Start(stream, null, true);
		await built.IsLive;
		var snapshot = await built.CaptureConsistentState();

		using var rebuilt = new RebuildingModel(_configured, snapshot);
		await rebuilt.IsLive;

		Assert.False(rebuilt.HasUnreplayableInput);
		Assert.Equal(4, rebuilt.Applied);
		AssertSelfConsistent(await rebuilt.CaptureConsistentState());
	}

	[Fact]
	public async Task a_capture_registered_while_a_model_is_being_disposed_is_released() {
		var stream = NewStream();
		Append(stream, 2);
		var rm = new CountingModel(_configured);
		rm.Start(stream, null, true);
		await rm.IsLive;

		rm.HoldOpenDispose();
		var disposing = Task.Run(rm.Dispose);
		Assert.True(rm.WaitForDisposeWindow(), "dispose never reached its window");

		// The pump is down and outstanding captures have been abandoned, but the model does not yet
		// report itself disposed — so this one is registered against a queue that will never run it.
		var capturing = rm.CaptureConsistentState();

		rm.LetDisposeFinish();
		await disposing;

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capturing);
	}
}
