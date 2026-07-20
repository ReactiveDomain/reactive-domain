using ReactiveDomain.Messaging.Bus;
using Xunit;

namespace ReactiveDomain.Messaging.Tests;

// Regression tests for the dispose-ordering races (#180/#218/#220) in the queued messaging
// components: queue/pump threads must be joined (or safely self-stopped) before state they
// dispatch into is torn down.
// ReSharper disable once InconsistentNaming
public sealed class when_disposing_queued_components {
	public record TestEvent : Event;

	[Fact]
	public void queued_handler_stop_from_its_own_thread_returns_promptly() {
		QueuedHandler? queue = null;
		var stopReturned = new ManualResetEventSlim(false);
		// ReSharper disable once AccessToModifiedClosure
		queue = new QueuedHandler(new AdHocHandler<IMessage>(_ => {
			queue!.Stop(); // pre-fix: joined its own thread and threw TimeoutException after 10s
			stopReturned.Set();
		}), "self-stop-queue");
		queue.Start();
		queue.Publish(new TestEvent());
		Assert.True(stopReturned.Wait(QueuedHandler.DefaultStopWaitTimeout),
			"Stop() called from the queue's own thread did not return.");
	}

	// A long idle poll interval paired with a short stop-wait timeout turns the #253 regression
	// into a deterministic outcome instead of a wall-clock race: pre-fix, Stop() cannot wake the
	// parked pump, so it blocks past the 2s stop-wait and throws TimeoutException; post-fix it
	// wakes the pump at once and returns. No timing threshold to flake under CI load.
	private static readonly TimeSpan LongIdlePoll = TimeSpan.FromSeconds(60);
	private static readonly TimeSpan ShortStopWait = TimeSpan.FromSeconds(2);

	[Fact]
	public void queued_handler_stop_wakes_the_parked_idle_pump() {
		var handled = new ManualResetEventSlim(false);
		var queue = new QueuedHandler(new AdHocHandler<IMessage>(_ => handled.Set()), "idle-stop-queue",
			threadStopWaitTimeout: ShortStopWait, idlePollInterval: LongIdlePoll);
		queue.Start();
		queue.Publish(new TestEvent());
		Assert.True(handled.Wait(QueuedHandler.DefaultStopWaitTimeout), "queue never handled the message");
		Assert.True(SpinWait.SpinUntil(() => queue.Idle, ShortStopWait), "pump never parked in its idle wait");
		queue.Stop(); // pre-fix (#253): parked pump not woken -> TimeoutException at the 2s stop-wait
	}

	[Fact]
	public void queued_handler_discarding_stop_wakes_the_parked_idle_pump() {
		var handled = new ManualResetEventSlim(false);
		var queue = new QueuedHandlerDiscarding(new AdHocHandler<IMessage>(_ => handled.Set()), "idle-stop-discarding",
			threadStopWaitTimeout: ShortStopWait, idlePollInterval: LongIdlePoll);
		queue.Start();
		queue.Publish(new TestEvent());
		Assert.True(handled.Wait(QueuedHandler.DefaultStopWaitTimeout), "queue never handled the message");
		Assert.True(SpinWait.SpinUntil(() => queue.Idle, ShortStopWait), "pump never parked in its idle wait");
		queue.Stop(); // pre-fix (#253): parked pump not woken -> TimeoutException at the 2s stop-wait
	}

	// Mirrors production QueuedSubscriber usage: the derived class disposes its own state
	// before calling base.Dispose(bool); no handler may run once that teardown begins.
	private sealed class DerivedStateSubscriber : QueuedSubscriber, IHandle<TestEvent> {
		private volatile bool _stateDisposed;
		public volatile bool HandlerSawDisposedState;
		public int Handled;

		public DerivedStateSubscriber(IBus bus) : base(bus) {
			Subscribe<TestEvent>(this);
		}

		public void Handle(TestEvent message) {
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
	public void no_subscriber_handler_runs_after_derived_dispose_begins() {
		using var bus = new InMemoryBus("external");
		var subscriber = new DerivedStateSubscriber(bus);
		for (var i = 0; i < 500; i++)
			bus.Publish(new TestEvent());
		SpinWait.SpinUntil(() => subscriber.Handled > 10, TimeSpan.FromSeconds(5));
		subscriber.Dispose();
		// Settle window: without the join an un-stopped pump would still be mid-handler here
		// and would observe the disposed state only after the assert had already passed.
		Thread.Sleep(250);
		Assert.False(subscriber.HandlerSawDisposedState,
			"Queue thread dispatched into state the derived subscriber had already disposed.");
	}

	[Fact]
	public void later_service_dispose_without_start_does_not_throw() {
		var later = new LaterService(new InMemoryBus("out"), TimeSource.System);
		later.Dispose(); // pump thread never started; must not throw or dispose in-use state
	}

	[Fact]
	public void later_service_dispose_under_load_does_not_crash_the_pump_thread() {
		var outbound = new InMemoryBus("out", false);
		var later = new LaterService(outbound, TimeSource.System);
		later.Start();
		// Saturate the pump with due messages, then dispose while it is mid-publish.
		// Pre-fix: Dispose waited only 100ms, then disposed the wait handle under the
		// still-running pump thread — an unhandled ObjectDisposedException that killed
		// the process (no test "fails"; the whole host dies).
		var now = TimeSource.System.Now();
		for (var i = 0; i < 5_000; i++)
			later.Handle(new DelaySendEnvelope(now, new TestEvent()));
		later.Dispose();
		// Give a straggler pump thread the chance to touch freshly disposed state before
		// the test host reports success; nothing to assert beyond process survival.
		Thread.Sleep(250);
	}
}
