using System.Reflection;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

/// <summary>
/// Covers the ReaderLock serialization contract on <see cref="TransientSubscriber"/>, and its parity
/// with the other subscriber bases.
/// </summary>
/// <remarks>
/// The behavioural test parks a handler mid-mutation, so "the state is torn right now" and "a reader
/// holding the lock cannot see it" are both facts about the pipeline rather than races. The negative
/// wait can only be beaten by a reader that acquired the lock while the handler still held it, which
/// is exactly the failure the contract forbids.
/// </remarks>
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

		var publishing = Task.Run(() => bus.Publish(new ReaderLockTestEvent()));
		Assert.True(entered.Wait(TestTimeouts.ThrottleWaitFor));

		// The handler is parked between the two mutations, so the pair really is inconsistent...
		Assert.Equal((1, 0), sut.ReadPairWithoutLock());
		// ...and a reader taking the lock cannot observe that.
		var read = Task.Run(sut.ReadPairUnderLock);
		Assert.NotSame(read, await Task.WhenAny(read, Task.Delay(BlockedProbe)));

		release.Set();
		Assert.Equal((1, 1), await read.WaitAsync(TestTimeouts.ThrottleWaitFor));
		await publishing.WaitAsync(TestTimeouts.ThrottleWaitFor);
		Assert.Equal(1, sut.Version);
	}

	[Fact]
	public async Task a_transient_subscriber_command_handler_runs_under_the_lock() {
		using var dispatcher = new Dispatcher(nameof(when_reading_subscriber_state_under_the_reader_lock));
		using var entered = new ManualResetEventSlim(false);
		using var release = new ManualResetEventSlim(false);
		using var sut = new PairCommandSubscriber(dispatcher, entered, release);

		var sending = Task.Run(() => dispatcher.Send(new ReaderLockTestCommand(), responseTimeout: TestTimeouts.ThrottleWaitFor));
		Assert.True(entered.Wait(TestTimeouts.ThrottleWaitFor));

		Assert.Equal((1, 0), sut.ReadPairWithoutLock());
		var read = Task.Run(sut.ReadPairUnderLock);
		Assert.NotSame(read, await Task.WhenAny(read, Task.Delay(BlockedProbe)));

		release.Set();
		Assert.Equal((1, 1), await read.WaitAsync(TestTimeouts.ThrottleWaitFor));
		await sending.WaitAsync(TestTimeouts.ThrottleWaitFor);
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
