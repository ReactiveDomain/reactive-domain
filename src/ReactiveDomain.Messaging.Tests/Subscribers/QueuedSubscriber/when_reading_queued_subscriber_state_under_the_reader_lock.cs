using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber;

// ReSharper disable once InconsistentNaming
public sealed class when_reading_queued_subscriber_state_under_the_reader_lock {
	// Only ever used to prove a wait did NOT complete; a broken lock fails it regardless of length.
	private static readonly TimeSpan BlockedProbe = TimeSpan.FromMilliseconds(100);

	[Fact]
	public async Task a_reader_holding_the_lock_sees_no_torn_state() {
		using var bus = new InMemoryBus(nameof(when_reading_queued_subscriber_state_under_the_reader_lock));
		using var entered = new ManualResetEventSlim(false);
		using var release = new ManualResetEventSlim(false);
		using var sut = new PairSubscriber(bus, entered, release);

		bus.Publish(new ReaderLockTestEvent());
		Assert.True(entered.Wait(TestTimeouts.ThrottleWaitFor));

		// The handler is parked between the two mutations, so the pair really is inconsistent...
		Assert.Equal((1, 0), sut.ReadPairWithoutLock());
		// ...and a reader taking the lock cannot observe that.
		var read = Task.Run(sut.ReadPairUnderLock);
		Assert.NotSame(read, await Task.WhenAny(read, Task.Delay(BlockedProbe)));

		release.Set();
		Assert.Equal((1, 1), await read.WaitAsync(TestTimeouts.ThrottleWaitFor));
		AssertEx.IsOrBecomesTrue(() => sut.Version == 1, TestTimeouts.ThrottleWaitFor);
	}

	private sealed class PairSubscriber : Bus.QueuedSubscriber, IHandle<ReaderLockTestEvent> {
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

	public record ReaderLockTestEvent : Event;
}
