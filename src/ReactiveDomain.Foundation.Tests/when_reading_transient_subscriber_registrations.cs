using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

/// <summary>
/// A transient subscriber registers onto a bus it shares, so its registry cannot be the bus's — it
/// has to answer from the registrations it made itself.
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class when_reading_transient_subscriber_registrations {
	[Fact]
	public void a_subscriber_reports_only_its_own_registrations() {
		using var bus = new InMemoryBus(nameof(when_reading_transient_subscriber_registrations));
		// Another handler on the same bus, which the subscriber must not claim.
		bus.Subscribe(new AdHocHandler<TransientRegistrationEventB>(_ => { }));
		using var sut = new EventSubscriber(bus);

		Assert.Equal([typeof(TransientRegistrationEventA)], sut.RegisteredMessageTypes.ToArray());
		Assert.Contains(typeof(TransientRegistrationEventB), bus.RegisteredMessageTypes);
	}

	/// <summary>An event registration takes the bus default, so it routes the derived types too.</summary>
	[Fact]
	public void an_event_registration_reports_the_types_it_routes() {
		using var bus = new InMemoryBus(nameof(when_reading_transient_subscriber_registrations));
		using var sut = new EventSubscriber(bus);

		Assert.Equal([typeof(TransientRegistrationEventA)], sut.RegisteredMessageTypes.ToArray());
		Assert.Equal(
			[typeof(TransientRegistrationEventA), typeof(TransientRegistrationEventADerived)],
			sut.HandledMessageTypes.OrderBy(t => t.Name).ToArray());
	}

	/// <summary>
	/// A command is registered for its exact type — one handler per command — so unlike an event
	/// registration it contributes nothing beyond itself.
	/// </summary>
	[Fact]
	public void a_command_registration_reports_only_its_own_type() {
		using var dispatcher = new Dispatcher(nameof(when_reading_transient_subscriber_registrations));
		using var sut = new CommandSubscriber(dispatcher);

		Assert.Equal([typeof(TransientRegistrationCommand)], sut.RegisteredMessageTypes.ToArray());
		// The derived command exists, so reporting only the base is a decision rather than an
		// artefact of there being nothing else to report.
		Assert.Equal([typeof(TransientRegistrationCommand)], sut.HandledMessageTypes.ToArray());
		Assert.DoesNotContain(typeof(TransientRegistrationCommandDerived), sut.HandledMessageTypes);
	}

	[Fact]
	public void the_two_kinds_of_registration_are_reported_together() {
		using var dispatcher = new Dispatcher(nameof(when_reading_transient_subscriber_registrations));
		using var sut = new BothKindsSubscriber(dispatcher);

		Assert.Equal(
			[typeof(TransientRegistrationCommand), typeof(TransientRegistrationEventA)],
			sut.RegisteredMessageTypes.OrderBy(t => t.Name).ToArray());
		Assert.Equal(
			[typeof(TransientRegistrationCommand), typeof(TransientRegistrationEventA),
				typeof(TransientRegistrationEventADerived)],
			sut.HandledMessageTypes.OrderBy(t => t.Name).ToArray());
	}

	/// <summary>Subscribing the same handler twice is one registration, so it is one entry.</summary>
	[Fact]
	public void a_repeated_registration_is_reported_once() {
		using var bus = new InMemoryBus(nameof(when_reading_transient_subscriber_registrations));
		using var sut = new TwiceSubscribedSubscriber(bus);

		Assert.Equal([typeof(TransientRegistrationEventA)], sut.RegisteredMessageTypes.ToArray());
	}

	/// <summary>Disposal drops the subscriptions, so the registry describing them cannot outlive them.</summary>
	[Fact]
	public void a_disposed_subscriber_reports_nothing() {
		using var bus = new InMemoryBus(nameof(when_reading_transient_subscriber_registrations));
		var sut = new EventSubscriber(bus);
		Assert.NotEmpty(sut.RegisteredMessageTypes);

		sut.Dispose();

		Assert.Empty(sut.RegisteredMessageTypes);
		Assert.Empty(sut.HandledMessageTypes);
	}

	/// <summary>Reporting registrations is a contract, not four coincidental members.</summary>
	[Fact]
	public void the_registry_is_reachable_through_the_interface() {
		using var bus = new InMemoryBus(nameof(when_reading_transient_subscriber_registrations));
		using var sut = new EventSubscriber(bus);
		IMessageRegistry registry = sut;

		Assert.Equal([typeof(TransientRegistrationEventA)], registry.RegisteredMessageTypes.ToArray());
		Assert.Contains(typeof(TransientRegistrationEventADerived), registry.HandledMessageTypes);
	}

	private sealed class EventSubscriber : TransientSubscriber, IHandle<TransientRegistrationEventA> {
		public EventSubscriber(IBus bus) : base(bus) => Subscribe<TransientRegistrationEventA>(this);
		public void Handle(TransientRegistrationEventA message) { }
	}

	private sealed class TwiceSubscribedSubscriber : TransientSubscriber, IHandle<TransientRegistrationEventA> {
		public TwiceSubscribedSubscriber(IBus bus) : base(bus) {
			Subscribe<TransientRegistrationEventA>(this);
			Subscribe<TransientRegistrationEventA>(this);
		}

		public void Handle(TransientRegistrationEventA message) { }
	}

	private sealed class CommandSubscriber : TransientSubscriber, IHandleCommand<TransientRegistrationCommand> {
		public CommandSubscriber(IDispatcher bus) : base(bus) => Subscribe<TransientRegistrationCommand>(this);
		public CommandResponse Handle(TransientRegistrationCommand command) => command.Succeed();
	}

	private sealed class BothKindsSubscriber : TransientSubscriber,
		IHandle<TransientRegistrationEventA>,
		IHandleCommand<TransientRegistrationCommand> {
		public BothKindsSubscriber(IDispatcher bus) : base(bus) {
			Subscribe<TransientRegistrationEventA>(this);
			Subscribe<TransientRegistrationCommand>(this);
		}

		public void Handle(TransientRegistrationEventA message) { }
		public CommandResponse Handle(TransientRegistrationCommand command) => command.Succeed();
	}

	public record TransientRegistrationEventA : Event;
	public record TransientRegistrationEventADerived : TransientRegistrationEventA;
	public record TransientRegistrationEventB : Event;
	public record TransientRegistrationCommand : Command;
	public record TransientRegistrationCommandDerived : TransientRegistrationCommand;
}
