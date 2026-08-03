using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

/// <summary>
/// Covers <see cref="ReadModelBase.RegisteredMessageTypes"/>: the subscription seam a completeness
/// test reflects over instead of scanning source for EventStream.Subscribe calls.
/// </summary>
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

	private sealed class RegistrationTestReadModel :
		ReadModelBase,
		IHandle<RegistrationTestEventA>,
		IHandle<RegistrationTestEventB> {
		public RegistrationTestReadModel() : base(nameof(RegistrationTestReadModel), new NullConfiguredConnection()) {
			// ReSharper disable RedundantTypeArgumentsOfMethod
			EventStream.Subscribe<RegistrationTestEventA>(this);
			EventStream.Subscribe<RegistrationTestEventB>(this);
			// ReSharper restore RedundantTypeArgumentsOfMethod
		}

		public void Handle(RegistrationTestEventA @event) { }
		public void Handle(RegistrationTestEventB @event) { }
	}

	private sealed class EmptyRegistrationTestReadModel() :
		ReadModelBase(nameof(EmptyRegistrationTestReadModel), new NullConfiguredConnection());

	public record RegistrationTestEventA : Event;
	public record RegistrationTestEventB : Event;
}
