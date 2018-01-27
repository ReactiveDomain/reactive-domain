using System;
using Xunit;

namespace ReactiveDomain
{
    namespace EventRouterTests
    {
        public class InstanceWithoutRoutes
        {
            private readonly EventRouter _sut;

            public InstanceWithoutRoutes()
            {
                _sut = new EventRouter();
            }

            [Fact]
            public void RegisterTypedRouteDoesNotAcceptNull()
            {
                Assert.Throws<ArgumentNullException>(
                    () => _sut.RegisterRoute<object>(null));
            }

            [Fact]
            public void RegisterUntypedRouteDoesNotAcceptNullForTypeOfEvent()
            {
                Assert.Throws<ArgumentNullException>(
                    () => _sut.RegisterRoute(null, _ => {}));
            }

            [Fact]
            public void RegisterUntypedRouteDoesNotAcceptNullForRoute()
            {
                Assert.Throws<ArgumentNullException>(
                    () => _sut.RegisterRoute(typeof(object), null));
            }

            [Fact]
            public void RegisterTypedRouteHasExpectedResult()
            {
                var route = new CapturingRoute();
                _sut.RegisterRoute<LocalEvent>(route.Capture);

                var @event = new LocalEvent();
                _sut.Route(@event);

                Assert.Equal(@event, route.Captured);
            }

            [Fact]
            public void RegisterUntypedRouteHasExpectedResult()
            {
                var route = new CapturingRoute();
                _sut.RegisterRoute(typeof(LocalEvent), route.Capture);

                var @event = new LocalEvent();
                _sut.Route(@event);

                Assert.Equal(@event, route.Captured);
            }

            [Fact]
            public void RouteDoesNotAcceptNull()
            {
                Assert.Throws<ArgumentNullException>(
                    () => _sut.Route(null));
            }

            [Fact]
            public void RouteEventWithoutRegisteredRouteDoesNotThrow()
            {
                var @event = new LocalEvent();
                _sut.Route(@event);
            }

            private class LocalEvent
            {
            }
        }

        public class InstanceWithRoutes
        {
            private readonly EventRouter _sut;

            private readonly CapturingRoute _registeredRoute;

            public InstanceWithRoutes()
            {
                _sut = new EventRouter();
                _registeredRoute = new CapturingRoute();

                _sut.RegisterRoute<EventWithRegisteredRoute>(_registeredRoute.Capture);
            }

            [Fact]
            public void RegisterTypedRouteDoesNotAcceptNull()
            {
                Assert.Throws<ArgumentNullException>(
                    () => _sut.RegisterRoute<object>(null));
            }

            [Fact]
            public void RegisterUntypedRouteDoesNotAcceptNullForTypeOfEvent()
            {
                Assert.Throws<ArgumentNullException>(
                    () => _sut.RegisterRoute(null, _ => {}));
            }

            [Fact]
            public void RegisterUntypedRouteDoesNotAcceptNullForRoute()
            {
                Assert.Throws<ArgumentNullException>(
                    () => _sut.RegisterRoute(typeof(object), null));
            }

            [Fact]
            public void RegisterTypedRouteForEventWithRegisteredRouteThrows()
            {
                Assert.Throws<InvalidOperationException>(
                    () => _sut.RegisterRoute<EventWithRegisteredRoute>(new CapturingRoute().Capture));
            }

            [Fact]
            public void RegisterUntypedRouteForEventWithRegisteredRouteThrows()
            {
                Assert.Throws<InvalidOperationException>(
                    () => _sut.RegisterRoute(typeof(EventWithRegisteredRoute), new CapturingRoute().Capture));
            }

            [Fact]
            public void RegisterTypedRouteForEventWithoutRegisteredRouteHasExpectedResult()
            {
                var route = new CapturingRoute();
                _sut.RegisterRoute<LocalEvent>(route.Capture);

                var @event = new LocalEvent();
                _sut.Route(@event);

                Assert.Equal(@event, route.Captured);
            }

            [Fact]
            public void RegisterUntypedRouteForEventWithoutRegisteredRouteHasExpectedResult()
            {
                var route = new CapturingRoute();
                _sut.RegisterRoute(typeof(LocalEvent), route.Capture);

                var @event = new LocalEvent();
                _sut.Route(@event);

                Assert.Equal(@event, route.Captured);
            }

            [Fact]
            public void RouteDoesNotAcceptNull()
            {
                Assert.Throws<ArgumentNullException>(
                    () => _sut.Route(null));
            }

            [Fact]
            public void RouteEventWithRegisteredRouteHasExpectedResult()
            {
                var @event = new EventWithRegisteredRoute();
                _sut.Route(@event);

                Assert.Equal(@event, _registeredRoute.Captured);
            }

            [Fact]
            public void RouteEventWithoutRegisteredRouteHasExpectedResult()
            {
                var @event = new LocalEvent();
                _sut.Route(@event);

                Assert.Null(_registeredRoute.Captured);
            }

            private class LocalEvent
            {
            }
            class EventWithRegisteredRoute
            {
            }
        }
    }
}
