using System;
using Xunit;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Domain.Tests
{
    namespace AggregateRootEntityTests
    {
        public class AnyInstance
        {
            // IEventSource behavior

            [Fact]
            public void IsEventSource()
            {
                Assert.IsAssignableFrom<IEventSource>(new AnyEntity());
            }

            [Fact]
            public void InitialExpectedVersionReturnsExpectedResult()
            {
                var sut = (IEventSource)new AnyEntity();
                Assert.Equal(ExpectedVersion.NoStream, sut.ExpectedVersion);
            }

            [Fact]
            public void ExpectedVersionRetainsValue()
            {
                var sut = (IEventSource)new AnyEntity();
                var value = new Random().Next();
                sut.ExpectedVersion = value;
                Assert.Equal(value, sut.ExpectedVersion);
            }

            // Routing behavior

            [Fact]
            public void NullForRouteOfTypedEventRouteNotAcceptable()
            {
                Assert.Throws<ArgumentNullException>(() => new RegisterNullTypedRouteEntity());
            }

            [Fact]
            public void NullForTypeOfEventOfUntypedEventRouteNotAcceptable()
            {
                Assert.Throws<ArgumentNullException>(() => new RegisterNullForTypeOfEventOfUntypedRouteEntity());
            }

            [Fact]
            public void NullForRouteOfUntypedEventRouteNotAcceptable()
            {
                Assert.Throws<ArgumentNullException>(() => new RegisterNullForRouteOfUntypedRouteEntity());
            }

            [Fact]
            public void RegisteringRepeatedTypedRouteNotAcceptable()
            {
                Assert.Throws<InvalidOperationException>(() => new RegisterRepeatedTypedRouteEntity());
            }

            [Fact]
            public void RegisteringRepeatedUntypedRouteNotAcceptable()
            {
                Assert.Throws<InvalidOperationException>(() => new RegisterRepeatedUntypedRouteEntity());
            }

            [Fact]
            public void RegisteringUntypedRouteAfterTypedRouteNotAcceptable()
            {
                Assert.Throws<InvalidOperationException>(() => new RegisterUntypedRouteAfterTypedRouteEntity());
            }

            [Fact]
            public void RegisteringTypedRouteAfterUntypedRouteNotAcceptable()
            {
                Assert.Throws<InvalidOperationException>(() => new RegisterTypedRouteAfterUntypedRouteEntity());
            }

            class AnyEntity : EventDrivenStateMachine { }

            class RegisterNullTypedRouteEntity : EventDrivenStateMachine
            {
                public RegisterNullTypedRouteEntity()
                {
                    Register<object>(null);
                }
            }

            class RegisterNullForTypeOfEventOfUntypedRouteEntity : EventDrivenStateMachine
            {
                public RegisterNullForTypeOfEventOfUntypedRouteEntity()
                {
                    Register(null, _ => {});
                }
            }

            class RegisterNullForRouteOfUntypedRouteEntity : EventDrivenStateMachine
            {
                public RegisterNullForRouteOfUntypedRouteEntity()
                {
                    Register(typeof(object), null);
                }
            }

            class RegisterRepeatedTypedRouteEntity : EventDrivenStateMachine
            {
                public RegisterRepeatedTypedRouteEntity()
                {
                    Register<object>(_ => { });
                    Register<object>(_ => { });
                }
            }

            class RegisterRepeatedUntypedRouteEntity : EventDrivenStateMachine
            {
                public RegisterRepeatedUntypedRouteEntity()
                {
                    Register(typeof(object), _ => { });
                    Register(typeof(object), _ => { });
                }
            }

            class RegisterUntypedRouteAfterTypedRouteEntity : EventDrivenStateMachine
            {
                public RegisterUntypedRouteAfterTypedRouteEntity()
                {
                    Register<object>(_ => { });
                    Register(typeof(object), _ => { });
                }
            }

            class RegisterTypedRouteAfterUntypedRouteEntity : EventDrivenStateMachine
            {
                public RegisterTypedRouteAfterUntypedRouteEntity()
                {
                    Register(typeof(object), _ => { });
                    Register<object>(_ => { });
                }
            }
        }

        public class ChangedInstance
        {
            private readonly EventDrivenStateMachine _sut;

            public ChangedInstance()
            {
                _sut = new ChangedEntity();
            }

            [Fact]
            public void RestoreFromEventsDoesNotAcceptNull()
            {
                var sut = (IEventSource) _sut;
                Assert.Throws<ArgumentNullException>(() => sut.RestoreFromEvents(null));
            }

            [Fact]
            public void RestoreFromEventsHasExpectedResult()
            {
                var sut = (IEventSource)_sut;
                Assert.Throws<InvalidOperationException>(
                    () => sut.RestoreFromEvents(new object[0]));
            }

            [Fact]
            public void TakeEventsHasExpectedResult()
            {
                var sut = (IEventSource)_sut;
                Assert.Equal(new object[] { ChangedEntity.Event }, sut.TakeEvents());
            }

            class ChangedEntity : EventDrivenStateMachine
            {
                public static readonly LocalEvent Event = new LocalEvent();

                public ChangedEntity()
                {
                    Raise(Event);
                }
            }

            private class LocalEvent
            {
            }
        }

        public class InstanceWithoutRoutes
        {
            private readonly EntityWithoutRoute _sut;

            public InstanceWithoutRoutes()
            {
                _sut = new EntityWithoutRoute();
            }

            [Fact]
            public void RestoreFromEventsDoesNotAcceptNull()
            {
                var sut = (IEventSource)_sut;
                Assert.Throws<ArgumentNullException>(() => sut.RestoreFromEvents(null));
            }

            [Fact]
            public void RestoreFromEventsHasExpectedResult()
            {
                var sut = (IEventSource)_sut;
                sut.RestoreFromEvents(new object[0]);

                Assert.Equal(new object[0], sut.TakeEvents());
            }

            [Fact]
            public void TakeEventsHasExpectedResult()
            {
                var sut = (IEventSource)_sut;
                Assert.Equal(new object[0], sut.TakeEvents());
            }

            [Fact]
            public void InvokingRaiseDoesNotAcceptNull()
            {
                Assert.Throws<ArgumentNullException>(() => _sut.InvokeRaise(null));
            }

            [Fact]
            public void InvokingRaiseHasExpectedResult()
            {
                var sut = (IEventSource)_sut;

                var @event = new LocalEvent();
                _sut.InvokeRaise(@event);

                Assert.Equal(new object[] { @event }, sut.TakeEvents());
            }

            class EntityWithoutRoute : EventDrivenStateMachine
            {
                public void InvokeRaise(object @event) { Raise(@event); }
            }

            private class LocalEvent
            {
            }
        }

        public class InstanceWithRoutes
        {
            private readonly EntityWithRoute _sut;

            public InstanceWithRoutes()
            {
                _sut = new EntityWithRoute();
            }

            [Fact]
            public void RestoreFromEventsDoesNotAcceptNull()
            {
                var sut = (IEventSource)_sut;
                Assert.Throws<ArgumentNullException>(() => sut.RestoreFromEvents(null));
            }

            [Fact]
            public void RestoreFromEventsHasExpectedResult()
            {
                var sut = (IEventSource)_sut;
                var @event = new LocalEvent();
                sut.RestoreFromEvents(new object[] { @event });

                Assert.Equal(new object[0], sut.TakeEvents());
                Assert.Equal(@event, _sut.Route.Captured);
            }

            [Fact]
            public void TakeEventsHasExpectedResult()
            {
                var sut = (IEventSource)_sut;
                Assert.Equal(new object[0], sut.TakeEvents());
            }

            [Fact]
            public void InvokingRaiseDoesNotAcceptNull()
            {
                Assert.Throws<ArgumentNullException>(() => _sut.InvokeRaise(null));
            }

            [Fact]
            public void InvokingRaiseHasExpectedResult()
            {
                var sut = (IEventSource)_sut;

                var @event = new LocalEvent();
                _sut.InvokeRaise(@event);

                Assert.Equal(new object[] { @event }, sut.TakeEvents());
                Assert.Equal(@event, _sut.Route.Captured);
            }

            class EntityWithRoute : EventDrivenStateMachine
            {
                public readonly CapturingRoute Route;

                public EntityWithRoute()
                {
                    Route = new CapturingRoute();

                    Register<LocalEvent>(Route.Capture);
                }

                public void InvokeRaise(object @event) { Raise(@event); }
            }

            private class LocalEvent
            {
            }
        }
    }
}
