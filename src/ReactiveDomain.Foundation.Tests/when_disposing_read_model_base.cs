using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using ReactiveDomain.Testing.EventStore;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

// Regression tests for the dispose-ordering races (#218/#220): the read model's queue
// thread must be provably stopped before any state a derived class disposes can be torn
// down, and disposing while the pump is draining must neither deadlock nor time out.
// ReSharper disable once InconsistentNaming
public sealed class when_disposing_read_model_base {
	private static readonly IEventSerializer _serializer = new JsonMessageSerializer();
	private static readonly IStreamNameBuilder _namer =
		new PrefixedCamelCaseStreamNameBuilder(nameof(when_disposing_read_model_base));

	public record ReadModelTestEvent : Event;

	private static IConfiguredConnection NewConnection() {
		var conn = new MockStreamStoreConnection(nameof(when_disposing_read_model_base));
		conn.Connect();
		return new ConfiguredConnection(conn, _namer, _serializer);
	}

	// Mirrors the shape of production read models: the derived class disposes its own
	// state (caches etc.) BEFORE calling base.Dispose(bool). The public Dispose() contract
	// is that no handler runs once that derived teardown begins.
	private sealed class DerivedStateReadModel : ReadModelBase, IHandle<ReadModelTestEvent> {
		private volatile bool _stateDisposed;
		public volatile bool HandlerSawDisposedState;
		public int Handled;

		public DerivedStateReadModel(IConfiguredConnection conn)
			: base(nameof(DerivedStateReadModel), conn) {
			EventStream.Subscribe<ReadModelTestEvent>(this);
		}

		public void Handle(ReadModelTestEvent @event) {
			Thread.Sleep(1); // keep the pump busy so Dispose always races a mid-drain queue
			if (_stateDisposed)
				HandlerSawDisposedState = true;
			Interlocked.Increment(ref Handled);
		}

		protected override void Dispose(bool disposing) {
			if (disposing)
				_stateDisposed = true;
			base.Dispose(disposing);
		}
	}

	[Fact]
	public void no_handler_runs_after_derived_dispose_begins() {
		var rm = new DerivedStateReadModel(NewConnection());
		for (var i = 0; i < 500; i++)
			rm.Publish(new ReadModelTestEvent());
		// Dispose mid-drain: the queue thread must be joined before the derived
		// Dispose(bool) marks its state disposed.
		AssertEx.IsOrBecomesTrue(() => rm.Handled > 10, 10_000);
		rm.Dispose();
		// Settle window: without the join an un-stopped pump would still be mid-handler here
		// and would observe the disposed state only after the assert had already passed.
		Thread.Sleep(250);
		Assert.False(rm.HandlerSawDisposedState, "Queue thread dispatched into state the derived class had already disposed.");
	}

	[Fact]
	public void dispose_while_draining_returns_promptly() {
		var rm = new DerivedStateReadModel(NewConnection());
		for (var i = 0; i < 500; i++)
			rm.Publish(new ReadModelTestEvent());
		var sw = System.Diagnostics.Stopwatch.StartNew();
		rm.Dispose(); // must not wait for the backlog, only for the in-flight message
		sw.Stop();
		Assert.True(sw.Elapsed < QueuedHandler.DefaultStopWaitTimeout,
			$"Dispose took {sw.Elapsed}; expected prompt stop without draining the backlog.");
	}

	private sealed class SelfDisposingReadModel : ReadModelBase, IHandle<ReadModelTestEvent> {
		public readonly ManualResetEventSlim DisposeReturned = new(false);
		public SelfDisposingReadModel(IConfiguredConnection conn)
			: base(nameof(SelfDisposingReadModel), conn) {
			EventStream.Subscribe<ReadModelTestEvent>(this);
		}
		public void Handle(ReadModelTestEvent @event) {
			// A dispose chain triggered from a handler runs on the queue thread itself;
			// stopping the queue must not attempt to join its own thread.
			Dispose();
			DisposeReturned.Set();
		}
	}

	[Fact]
	public void dispose_from_the_queue_thread_does_not_deadlock() {
		var rm = new SelfDisposingReadModel(NewConnection());
		rm.Publish(new ReadModelTestEvent());
		Assert.True(rm.DisposeReturned.Wait(QueuedHandler.DefaultStopWaitTimeout),
			"Dispose called from the read model's own queue thread did not return.");
	}

	[Fact]
	public void no_handler_runs_after_derived_dispose_begins_while_reading_history() {
		var conn = new MockStreamStoreConnection(nameof(when_disposing_read_model_base));
		conn.Connect();
		var stream = _namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());
		for (var i = 0; i < 500; i++)
			conn.AppendToStream(stream, ExpectedVersion.Any, null, _serializer.Serialize(new ReadModelTestEvent()));

		var rm = new DerivedStateReadModel(new ConfiguredConnection(conn, _namer, _serializer));
		// The reader delivers the historical events into the same queue the listeners feed;
		// dispose mid-read must give the same guarantee as dispose mid-listen.
		rm.StartAsync(stream);
		// generous timeout: the read task + slow handler are at the mercy of CI scheduling
		AssertEx.IsOrBecomesTrue(() => rm.Handled > 10, 10_000);
		rm.Dispose();
		// Settle window: without the join an un-stopped pump would still be mid-handler here
		// and would observe the disposed state only after the assert had already passed.
		Thread.Sleep(250);
		Assert.False(rm.HandlerSawDisposedState, "Queue thread dispatched into state the derived class had already disposed.");
	}
}
