using System.Reflection;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

// ReSharper disable once InconsistentNaming
public sealed class when_reading_subscriber_state_under_the_reader_lock {
	// Only ever used to prove a wait did NOT complete; a broken lock fails it regardless of length.
	private static readonly TimeSpan BlockedProbe = TimeSpan.FromMilliseconds(100);

	[Fact]
	public async Task a_transient_subscriber_reader_holding_the_lock_sees_no_torn_state() {
		using var bus = new InMemoryBus(nameof(when_reading_subscriber_state_under_the_reader_lock));
		using var entered = new ManualResetEventSlim(false);
		using var release = new ManualResetEventSlim(false);
		using var sut = new PairSubscriber(bus, entered, release);

		var publishing = Blocking(() => bus.Publish(new ReaderLockTestEvent()));
		Assert.True(entered.Wait(TestTimeouts.ThrottleWaitFor));

		// The handler is parked between the two mutations, so the pair really is inconsistent...
		Assert.Equal((1, 0), sut.ReadPairWithoutLock());
		// ...and a reader taking the lock cannot observe that.
		var read = Reading(sut.ReadPairUnderLock);
		Assert.NotSame(read, await Task.WhenAny(read, Task.Delay(BlockedProbe)));

		release.Set();
		Assert.Equal((1, 1), await read.WaitAsync(TestTimeouts.ThrottleWaitFor));
		Assert.True(publishing.Join(TestTimeouts.ThrottleWaitFor));
		Assert.Equal(1, sut.Version);
	}

	/// <summary>
	/// A dedicated thread, not the pool: the handler parks inside the lock for as long as the test needs
	/// it to, and a parked pool thread is one the reader below may then be unable to get. Under a loaded
	/// run that shows up as this test timing out waiting to enter the handler at all.
	/// </summary>
	private static Thread Blocking(Action work) {
		var thread = new Thread(() => work()) { IsBackground = true };
		thread.Start();
		return thread;
	}

	/// <summary>
	/// The reader, on its own thread, returned as a task that is pending only because the lock is held.
	/// This does not return until the thread is running, so the "did not complete" probe below cannot
	/// pass merely because the reader was never scheduled — which on the pool it could.
	/// </summary>
	private static Task<(int, int)> Reading(Func<(int, int)> read) {
		var running = new ManualResetEventSlim(false);
		var result = new TaskCompletionSource<(int, int)>(TaskCreationOptions.RunContinuationsAsynchronously);
		Blocking(() => {
			running.Set();
			try {
				result.SetResult(read());
			} catch (Exception ex) {
				result.SetException(ex);
			}
		});
		Assert.True(running.Wait(TestTimeouts.ThrottleWaitFor));
		return result.Task;
	}

	[Fact]
	public async Task a_transient_subscriber_command_handler_runs_under_the_lock() {
		using var dispatcher = new Dispatcher(nameof(when_reading_subscriber_state_under_the_reader_lock));
		using var entered = new ManualResetEventSlim(false);
		using var release = new ManualResetEventSlim(false);
		using var sut = new PairCommandSubscriber(dispatcher, entered, release);

		var sending = Blocking(() => dispatcher.Send(new ReaderLockTestCommand(), responseTimeout: TestTimeouts.ThrottleWaitFor));
		Assert.True(entered.Wait(TestTimeouts.ThrottleWaitFor));

		Assert.Equal((1, 0), sut.ReadPairWithoutLock());
		var read = Reading(sut.ReadPairUnderLock);
		Assert.NotSame(read, await Task.WhenAny(read, Task.Delay(BlockedProbe)));

		release.Set();
		Assert.Equal((1, 1), await read.WaitAsync(TestTimeouts.ThrottleWaitFor));
		Assert.True(sending.Join(TestTimeouts.ThrottleWaitFor));
		Assert.Equal(1, sut.Version);
	}

	[Theory]
	[InlineData(typeof(ReadModelBase))]
	[InlineData(typeof(TransientSubscriber))]
	[InlineData(typeof(QueuedSubscriber))]
	public void every_subscriber_base_exposes_the_same_reader_lock_contract(Type subscriberBase) {
		var readerLock = subscriberBase.GetField("ReaderLock", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(readerLock);
		Assert.True(readerLock.IsFamily, $"{subscriberBase.Name}.ReaderLock must be protected.");
		Assert.True(readerLock.IsInitOnly, $"{subscriberBase.Name}.ReaderLock must be readonly.");
		Assert.Equal(typeof(object), readerLock.FieldType);

		var version = subscriberBase.GetProperty("Version", BindingFlags.Instance | BindingFlags.Public);
		Assert.NotNull(version);
		Assert.Equal(typeof(int), version.PropertyType);
		Assert.NotNull(version.GetMethod);
		Assert.True(version.SetMethod is null or { IsPublic: false },
			$"{subscriberBase.Name}.Version must not be publicly settable.");
	}

	/// <summary>The wrapper must reach the bus as the same object, or the bus cannot match the two.</summary>
	[Fact]
	public void subscribing_the_same_handler_twice_registers_once() {
		using var bus = new InMemoryBus(nameof(when_reading_subscriber_state_under_the_reader_lock));
		using var sut = new TwiceSubscribedSubscriber(bus);

		bus.Publish(new ReaderLockTestEvent());

		Assert.Equal(1, sut.Handled);
		Assert.Equal(1, sut.Version);
	}

	private sealed class TwiceSubscribedSubscriber : TransientSubscriber, IHandle<ReaderLockTestEvent> {
		public int Handled;

		public TwiceSubscribedSubscriber(IBus bus) : base(bus) {
			Subscribe<ReaderLockTestEvent>(this);
			Subscribe<ReaderLockTestEvent>(this);
		}

		public void Handle(ReaderLockTestEvent message) => Interlocked.Increment(ref Handled);
	}

	/// <summary>One object can be both sorts of handler for one message, and each sort needs its own
	/// wrapper. Finding the wrong sort must not count as not having found one — the sort that looks
	/// second would then make a new wrapper every time, and the bus, which matches on the wrapper,
	/// would register each as another handler.</summary>
	[Fact]
	public void subscribing_a_handler_that_is_both_sorts_registers_each_once() {
		using var bus = new Dispatcher(nameof(when_reading_subscriber_state_under_the_reader_lock));
		using var sut = new DualRoleSubscriber(bus);

		bus.Publish(new ReaderLockTestCommand());

		Assert.Equal(1, sut.Handled);
	}

	private sealed class DualRoleSubscriber :
		TransientSubscriber, IHandle<ReaderLockTestCommand>, IHandleCommand<ReaderLockTestCommand> {
		public int Handled;

		public DualRoleSubscriber(IDispatcher bus) : base(bus) {
			// The command sort first, so the event sort's lookup meets it. A command takes one handler
			// and the bus says so, which is why only the event sort is subscribed twice.
			Subscribe<ReaderLockTestCommand>((IHandleCommand<ReaderLockTestCommand>)this);
			Subscribe<ReaderLockTestCommand>((IHandle<ReaderLockTestCommand>)this);
			Subscribe<ReaderLockTestCommand>((IHandle<ReaderLockTestCommand>)this);
		}

		public void Handle(ReaderLockTestCommand message) => Interlocked.Increment(ref Handled);

		CommandResponse IHandleCommand<ReaderLockTestCommand>.Handle(ReaderLockTestCommand command) =>
			command.Succeed();
	}

	private sealed class PairSubscriber : TransientSubscriber, IHandle<ReaderLockTestEvent> {
		private readonly ManualResetEventSlim _entered;
		private readonly ManualResetEventSlim _release;
		private int _first;
		private int _second;

		public PairSubscriber(IBus bus, ManualResetEventSlim entered, ManualResetEventSlim release) : base(bus) {
			_entered = entered;
			_release = release;
			// ReSharper disable once RedundantTypeArgumentsOfMethod
			Subscribe<ReaderLockTestEvent>(this);
		}

		public void Handle(ReaderLockTestEvent message) {
			_first++;
			_entered.Set();
			_release.Wait(TestTimeouts.ThrottleWaitFor);
			_second++;
		}

		public (int, int) ReadPairUnderLock() {
			lock (ReaderLock) { return (_first, _second); }
		}

		public (int, int) ReadPairWithoutLock() => (Volatile.Read(ref _first), Volatile.Read(ref _second));
	}

	private sealed class PairCommandSubscriber : TransientSubscriber, IHandleCommand<ReaderLockTestCommand> {
		private readonly ManualResetEventSlim _entered;
		private readonly ManualResetEventSlim _release;
		private int _first;
		private int _second;

		public PairCommandSubscriber(IDispatcher bus, ManualResetEventSlim entered, ManualResetEventSlim release)
			: base(bus) {
			_entered = entered;
			_release = release;
			// ReSharper disable once RedundantTypeArgumentsOfMethod
			Subscribe<ReaderLockTestCommand>(this);
		}

		public CommandResponse Handle(ReaderLockTestCommand command) {
			_first++;
			_entered.Set();
			_release.Wait(TestTimeouts.ThrottleWaitFor);
			_second++;
			return command.Succeed();
		}

		public (int, int) ReadPairUnderLock() {
			lock (ReaderLock) { return (_first, _second); }
		}

		public (int, int) ReadPairWithoutLock() => (Volatile.Read(ref _first), Volatile.Read(ref _second));
	}

	public record ReaderLockTestEvent : Event;
	public record ReaderLockTestCommand : Command;
}
