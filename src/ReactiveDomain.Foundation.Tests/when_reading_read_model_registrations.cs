using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using ReactiveDomain.Testing.EventStore;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

// ReSharper disable once InconsistentNaming
public sealed class when_reading_read_model_registrations {
	[Fact]
	public void a_read_model_reports_exactly_the_types_its_handlers_registered() {
		using var rm = new RegistrationTestReadModel();

		Assert.Equal(
			[typeof(RegistrationTestEventA), typeof(RegistrationTestEventB)],
			rm.RegisteredMessageTypes.OrderBy(t => t.Name).ToArray());
	}

	[Fact]
	public void a_read_model_with_no_handlers_reports_none() {
		using var rm = new EmptyRegistrationTestReadModel();

		Assert.Empty(rm.RegisteredMessageTypes);
	}

	/// <summary>The queue's subscription to the listener is on a different bus, and is not a handler.</summary>
	[Fact]
	public void a_live_listener_does_not_add_a_registration() {
		var namer = new PrefixedCamelCaseStreamNameBuilder(nameof(when_reading_read_model_registrations));
		var serializer = new JsonMessageSerializer();
		var conn = new MockStreamStoreConnection(nameof(when_reading_read_model_registrations));
		conn.Connect();

		var stream = namer.GenerateForAggregate(typeof(RegistrationTestAggregate), Guid.NewGuid());
		conn.AppendToStream(stream, ExpectedVersion.Any, null, serializer.Serialize(new RegistrationTestEventA()));

		using var rm = new RegistrationTestReadModel(new ConfiguredConnection(conn, namer, serializer));
		rm.StartAsync(stream);

		// The listener is live once the model has folded the event, so the plumbing subscription
		// this test is about definitely exists by the time the assertion runs.
		AssertEx.IsOrBecomesTrue(() => rm.Handled > 0, 5_000);
		Assert.Single(rm.GetCheckpoint());

		Assert.Equal(
			[typeof(RegistrationTestEventA), typeof(RegistrationTestEventB)],
			rm.RegisteredMessageTypes.OrderBy(t => t.Name).ToArray());
	}

	private sealed class RegistrationTestAggregate : AggregateRoot;

	private sealed class RegistrationTestReadModel :
		ReadModelBase,
		IHandle<RegistrationTestEventA>,
		IHandle<RegistrationTestEventB> {
		public RegistrationTestReadModel(IConfiguredConnection? conn = null)
			: base(nameof(RegistrationTestReadModel), conn ?? new NullConfiguredConnection()) {
			// ReSharper disable RedundantTypeArgumentsOfMethod
			EventStream.Subscribe<RegistrationTestEventA>(this);
			EventStream.Subscribe<RegistrationTestEventB>(this);
			// ReSharper restore RedundantTypeArgumentsOfMethod
		}

		public int Handled;

		public void Handle(RegistrationTestEventA @event) => Interlocked.Increment(ref Handled);
		public void Handle(RegistrationTestEventB @event) { }
	}

	private sealed class EmptyRegistrationTestReadModel() :
		ReadModelBase(nameof(EmptyRegistrationTestReadModel), new NullConfiguredConnection());

	public record RegistrationTestEventA : Event;
	public record RegistrationTestEventB : Event;
}
