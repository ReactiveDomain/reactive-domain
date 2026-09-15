using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests.Domain;

public sealed class when_routing_unregistered_events {
	[Fact]
	public void an_unregistered_raise_is_recorded_and_not_folded() {
		var sut = new QuietEntity();
		sut.RaiseUnregistered();
		Assert.Single(((IEventSource)sut).TakeEvents());
		Assert.Equal(0, sut.Folded);
	}

	[Fact]
	public void throw_on_unrouted_fails_the_raise() {
		var sut = new QuietEntity { Loud = true };
		Assert.Throws<UnroutedEventException>(sut.RaiseUnregistered);
		Assert.Empty(((IEventSource)sut).TakeEvents());
	}

	[Fact]
	public void the_quiet_default_lets_an_unregistered_raise_through() {
		var sut = new QuietEntity { Loud = false };
		sut.RaiseUnregistered();
		Assert.Single(((IEventSource)sut).TakeEvents());
	}

	[Fact]
	public void a_pass_through_is_recorded_and_not_folded_even_when_loud() {
		var sut = new QuietEntity { Loud = true };
		sut.RaisePassThrough();
		Assert.Single(((IEventSource)sut).TakeEvents());
		Assert.Equal(0, sut.Folded);
	}

	[Fact]
	public void the_route_table_covers_registered_and_pass_through_types() {
		AggregateRoutes.AssertCovered(
			new QuietEntity(),
			[typeof(Folded), typeof(PassThrough)]);
	}

	[Fact]
	public void missing_raised_types_fail_the_guard() {
		var sut = new QuietEntity();
		var error = Record.Exception(() => AggregateRoutes.AssertCovered(sut, [typeof(Unregistered)]));
		Assert.NotNull(error);
		Assert.Contains("Unregistered", error.Message);
	}

	private sealed class QuietEntity : EventDrivenStateMachine {
		public int Folded { get; private set; }
		public bool Loud { set => ThrowOnUnrouted = value; }

		public QuietEntity() {
			Register<Folded>(_ => Folded++);
			RegisterPassThrough<PassThrough>();
		}

		public void RaiseUnregistered() => Raise(new Unregistered());
		public void RaisePassThrough() => Raise(new PassThrough());
	}

	private sealed class Folded;
	private sealed class PassThrough;
	private sealed class Unregistered;
}
