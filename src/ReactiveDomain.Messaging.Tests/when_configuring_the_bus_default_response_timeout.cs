using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests;

/// <summary>
/// The bus-level default response timeout. A send site can always pass its own timeout, but a
/// send nested inside a command handler has no send site to pass one from — it gets whatever
/// default the bus was built with. These tests pin both halves: unset is exactly the historical
/// 500 ms, and a configured value is what an un-timed send (nested or not) is measured against.
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class when_configuring_the_bus_default_response_timeout {
	// Longer than any default under test, so a command that is not released early can only end
	// by timing out. The handler is released as soon as the assertion is made, so the block is
	// an upper bound, not a cost every run pays.
	private static readonly TimeSpan BlockPastEveryDefault = TimeSpan.FromSeconds(3);

	// Longer than the historical 500 ms default and shorter than the configured default, so it
	// separates the two: it times out on an unconfigured bus and succeeds on a configured one.
	private static readonly TimeSpan BlockPastTheHistoricalDefault = TimeSpan.FromSeconds(1);

	private static readonly TimeSpan ConfiguredDefault = TimeSpan.FromSeconds(5);

	// Generous, explicit, and applied only to the *outer* command of the nested cases, so the
	// outer send cannot time out first and mask what the nested send did.
	private static readonly TimeSpan OuterSendTimeout = TimeSpan.FromSeconds(20);

	// Three queues, matching the existing chained-command tests: a nested send needs a queue
	// thread other than the one its blocked caller is running on.
	private const uint QueueCount = 3;

	public record SlowCommand : Command;
	public record OuterCommand : Command;

	private sealed class Bus :
		IHandleCommand<SlowCommand>,
		IHandleCommand<OuterCommand>,
		IDisposable {
		private readonly IDispatcher _bus;
		private readonly TimeSpan _block;
		private long _released;

		public Bus(string name, TimeSpan block, TimeSpan? defaultResponseTimeout = null) {
			_block = block;
			_bus = new Dispatcher(name, QueueCount, defaultResponseTimeout: defaultResponseTimeout);
			_bus.Subscribe<SlowCommand>(this);
			_bus.Subscribe<OuterCommand>(this);
		}

		/// <summary>Sends the slow command with no timeout, so the bus default decides.</summary>
		public void SendSlow() {
			try {
				_bus.Send(new SlowCommand());
			} finally {
				Release();
			}
		}

		/// <summary>
		/// Sends the outer command with an explicit, generous timeout. Its handler sends the slow
		/// command with none, so only the nested send is measured against the bus default.
		/// </summary>
		public void SendOuter() {
			try {
				_bus.Send(new OuterCommand(), responseTimeout: OuterSendTimeout);
			} finally {
				Release();
			}
		}

		/// <summary>Lets a blocked handler return instead of running out its full block.</summary>
		private void Release() => Interlocked.Exchange(ref _released, 1);

		public CommandResponse Handle(SlowCommand command) {
			SpinWait.SpinUntil(() => Interlocked.Read(ref _released) == 1, _block);
			return command.Succeed();
		}

		public CommandResponse Handle(OuterCommand command) {
			// No timeout passed: this is the nested send that can only use the bus default.
			_bus.Send(MessageBuilder.From(command).Build(() => new SlowCommand()));
			return command.Succeed();
		}

		public void Dispose() {
			Release();
			_bus.Dispose();
		}
	}

	[Fact]
	public void the_unconfigured_default_is_the_historical_value() {
		Assert.Equal(TimeSpan.FromMilliseconds(500), CommandManager.DefaultResponseTimeout);
	}

	[Fact]
	public void a_default_response_timeout_must_be_positive() {
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			new Dispatcher(nameof(a_default_response_timeout_must_be_positive), QueueCount,
				defaultResponseTimeout: TimeSpan.Zero));
	}

	[Fact]
	public void an_unconfigured_bus_times_out_at_the_historical_default() {
		using var bus = new Bus(
			nameof(an_unconfigured_bus_times_out_at_the_historical_default),
			BlockPastEveryDefault);

		Assert.Throws<CommandTimedOutException>(bus.SendSlow);
	}

	[Fact]
	public void a_configured_bus_times_sends_against_the_configured_default() {
		using var bus = new Bus(
			nameof(a_configured_bus_times_sends_against_the_configured_default),
			BlockPastTheHistoricalDefault,
			ConfiguredDefault);

		// Would have thrown CommandTimedOutException on an unconfigured bus — see the test above.
		bus.SendSlow();
	}

	[Fact]
	public void an_unconfigured_bus_times_nested_sends_at_the_historical_default() {
		using var bus = new Bus(
			nameof(an_unconfigured_bus_times_nested_sends_at_the_historical_default),
			BlockPastEveryDefault);

		// The nested send times out; its exception is what fails the outer command.
		AssertEx.CommandThrows<CommandTimedOutException>(bus.SendOuter);
	}

	[Fact]
	public void a_configured_bus_times_nested_sends_against_the_configured_default() {
		using var bus = new Bus(
			nameof(a_configured_bus_times_nested_sends_against_the_configured_default),
			BlockPastTheHistoricalDefault,
			ConfiguredDefault);

		bus.SendOuter();
	}
}
