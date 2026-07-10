using Xunit;

namespace ReactiveDomain.Foundation.Tests.Domain;

public class AnyInstance {
	// IEventSource behavior

	[Fact]
	public void IsEventSource() {
		Assert.IsAssignableFrom<IEventSource>(new AnyEntity());
	}

	[Fact]
	public void InitialExpectedVersionReturnsExpectedResult() {
		IEventSource sut = new AnyEntity();
		Assert.Equal(ExpectedVersion.NoStream, sut.ExpectedVersion);
	}

	[Fact]
	public void ExpectedVersionRetainsValue() {
		IEventSource sut = new AnyEntity();
		var value = new Random().Next();
		sut.ExpectedVersion = value;
		Assert.Equal(value, sut.ExpectedVersion);
	}

	// Routing behavior

	[Fact]
	public void NullForRouteOfTypedEventRouteNotAcceptable() {
		Assert.Throws<ArgumentNullException>(() => new RegisterNullTypedRouteEntity());
	}

	[Fact]
	public void NullForTypeOfEventOfUntypedEventRouteNotAcceptable() {
		Assert.Throws<ArgumentNullException>(() => new RegisterNullForTypeOfEventOfUntypedRouteEntity());
	}

	[Fact]
	public void NullForRouteOfUntypedEventRouteNotAcceptable() {
		Assert.Throws<ArgumentNullException>(() => new RegisterNullForRouteOfUntypedRouteEntity());
	}

	[Fact]
	public void RegisteringRepeatedTypedRouteNotAcceptable() {
		Assert.Throws<InvalidOperationException>(() => new RegisterRepeatedTypedRouteEntity());
	}

	[Fact]
	public void RegisteringRepeatedUntypedRouteNotAcceptable() {
		Assert.Throws<InvalidOperationException>(() => new RegisterRepeatedUntypedRouteEntity());
	}

	[Fact]
	public void RegisteringUntypedRouteAfterTypedRouteNotAcceptable() {
		Assert.Throws<InvalidOperationException>(() => new RegisterUntypedRouteAfterTypedRouteEntity());
	}

	[Fact]
	public void RegisteringTypedRouteAfterUntypedRouteNotAcceptable() {
		Assert.Throws<InvalidOperationException>(() => new RegisterTypedRouteAfterUntypedRouteEntity());
	}

	private class AnyEntity : EventDrivenStateMachine;

	private class RegisterNullTypedRouteEntity : EventDrivenStateMachine {
		public RegisterNullTypedRouteEntity() {
			Register<object>(null!);
		}
	}

	private class RegisterNullForTypeOfEventOfUntypedRouteEntity : EventDrivenStateMachine {
		public RegisterNullForTypeOfEventOfUntypedRouteEntity() {
			Register(null!, _ => { });
		}
	}

	private class RegisterNullForRouteOfUntypedRouteEntity : EventDrivenStateMachine {
		public RegisterNullForRouteOfUntypedRouteEntity() {
			Register(typeof(object), null!);
		}
	}

	private class RegisterRepeatedTypedRouteEntity : EventDrivenStateMachine {
		public RegisterRepeatedTypedRouteEntity() {
			Register<object>(_ => { });
			Register<object>(_ => { });
		}
	}

	private class RegisterRepeatedUntypedRouteEntity : EventDrivenStateMachine {
		public RegisterRepeatedUntypedRouteEntity() {
			Register(typeof(object), _ => { });
			Register(typeof(object), _ => { });
		}
	}

	private class RegisterUntypedRouteAfterTypedRouteEntity : EventDrivenStateMachine {
		public RegisterUntypedRouteAfterTypedRouteEntity() {
			Register<object>(_ => { });
			Register(typeof(object), _ => { });
		}
	}

	private class RegisterTypedRouteAfterUntypedRouteEntity : EventDrivenStateMachine {
		public RegisterTypedRouteAfterUntypedRouteEntity() {
			Register(typeof(object), _ => { });
			Register<object>(_ => { });
		}
	}
}

public class ChangedInstance {
	private readonly EventDrivenStateMachine _sut = new ChangedEntity();

	[Fact]
	public void RestoreFromEventsDoesNotAcceptNull() {
		IEventSource sut = _sut;
		Assert.Throws<ArgumentNullException>(() => sut.RestoreFromEvents(null!));
	}

	[Fact]
	public void RestoreFromEventsHasExpectedResult() {
		IEventSource sut = _sut;
		Assert.Throws<InvalidOperationException>(() => sut.RestoreFromEvents([]));
	}

	[Fact]
	public void TakeEventsHasExpectedResult() {
		IEventSource sut = _sut;
		Assert.Equal(-1, sut.ExpectedVersion);
		Assert.Equal([ChangedEntity.Event], sut.TakeEvents());
		Assert.Equal(0, sut.ExpectedVersion);
	}

	private class ChangedEntity : EventDrivenStateMachine {
		public static readonly LocalEvent Event = new();

		public ChangedEntity() {
			Raise(Event);
		}
	}

	private class LocalEvent;
}

public class InstanceWithoutRoutes {
	private readonly EntityWithoutRoute _sut = new();

	[Fact]
	public void RestoreFromEventsDoesNotAcceptNull() {
		IEventSource sut = _sut;
		Assert.Throws<ArgumentNullException>(() => sut.RestoreFromEvents(null!));
	}

	[Fact]
	public void RestoreFromEventsHasExpectedResult() {
		IEventSource sut = _sut;
		sut.RestoreFromEvents([]);

		Assert.Equal([], sut.TakeEvents());
	}

	[Fact]
	public void TakeEventsHasExpectedResult() {
		IEventSource sut = _sut;
		Assert.Equal(-1, sut.ExpectedVersion);
		Assert.Equal([], sut.TakeEvents());
		Assert.Equal(-1, sut.ExpectedVersion);
	}

	[Fact]
	public void InvokingRaiseDoesNotAcceptNull() {
		Assert.Throws<ArgumentNullException>(() => _sut.InvokeRaise(null!));
	}

	[Fact]
	public void InvokingRaiseHasExpectedResult() {
		IEventSource sut = _sut;

		var @event = new LocalEvent();
		_sut.InvokeRaise(@event);

		Assert.Equal([@event], sut.TakeEvents());
	}

	private class EntityWithoutRoute : EventDrivenStateMachine {
		public void InvokeRaise(object @event) { Raise(@event); }
	}

	private class LocalEvent;
}

public class InstanceWithRoutes {
	private readonly EntityWithRoute _sut = new();

	[Fact]
	public void RestoreFromEventsDoesNotAcceptNull() {
		IEventSource sut = _sut;
		Assert.Throws<ArgumentNullException>(() => sut.RestoreFromEvents(null!));
	}

	[Fact]
	public void RestoreFromEventsHasExpectedResult() {
		IEventSource sut = _sut;
		var @event = new LocalEvent();
		sut.RestoreFromEvents([@event]);

		Assert.Equal([], sut.TakeEvents());
		Assert.Equal(@event, _sut.Route.Captured);
	}

	[Fact]
	public void TakeEventsHasExpectedResult() {
		IEventSource sut = _sut;
		Assert.Equal(-1, sut.ExpectedVersion);
		Assert.Equal([], sut.TakeEvents());
		Assert.Equal(-1, sut.ExpectedVersion);
	}

	[Fact]
	public void InvokingRaiseDoesNotAcceptNull() {
		Assert.Throws<ArgumentNullException>(() => _sut.InvokeRaise(null!));
	}

	[Fact]
	public void InvokingRaiseHasExpectedResult() {
		IEventSource sut = _sut;

		var @event = new LocalEvent();
		_sut.InvokeRaise(@event);

		Assert.Equal([@event], sut.TakeEvents());
		Assert.Equal(@event, _sut.Route.Captured);
	}

	private class EntityWithRoute : EventDrivenStateMachine {
		public readonly CapturingRoute Route;

		public EntityWithRoute() {
			Route = new CapturingRoute();

			Register<LocalEvent>(Route.Capture);
		}

		public void InvokeRaise(object @event) { Raise(@event); }
	}

	private class LocalEvent;
}
