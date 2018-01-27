using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Xunit;

namespace ReactiveDomain
{
    public class Usage
    {
        [Fact]
        public void Show()
        {
            var sut = new HasRoutesDiscoveredByConvention();
            var event1 = new Event1();
            var event2 = new Event2();
            sut.InvokeApply(event1);
            sut.InvokeApply(event2);
            Assert.Equal(event1, sut.RouteForEvent1.Captured);
            Assert.Equal(event2, sut.RouteForEvent2.Captured);
        }

        class HasRoutesDiscoveredByConvention : AggregateRootEntity
        {
            public readonly CapturingRoute RouteForEvent1 = new CapturingRoute();
            public readonly CapturingRoute RouteForEvent2 = new CapturingRoute();

            public HasRoutesDiscoveredByConvention()
            {
                this.DiscoverRoutes(this.Register);
            }

            public void InvokeApply(object @event)
            {
                Raise(@event);
            }

            void Apply(Event1 msg) {
                RouteForEvent1.Capture(msg);
            }

            void Apply(Event2 msg) {
                RouteForEvent2.Capture(msg);                    
            }
        }

        class Event1 {}
        class Event2 {}
    }

    public static class EventSourceExtensions
    {
        private static readonly ConcurrentDictionary<Type, Func<IEventSource, Tuple<Type, Action<object>>>[]> RouteFactoryIndex =
            new ConcurrentDictionary<Type, Func<IEventSource, Tuple<Type, Action<object>>>[]>();

        // This method discovers all private Apply methods on an event source that take an event as the sole parameter and return void
        // and registers them with the given source. The result of the discovery process is cached.
        public static void DiscoverRoutes(this IEventSource source, Action<Type, Action<object>> register)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (register == null)
            {
                throw new ArgumentNullException(nameof(register));
            }
            
            var routeFactories = RouteFactoryIndex.GetOrAdd(source.GetType(), typeOfSource => {
                var builder = new List<Func<IEventSource, Tuple<Type, Action<object>>>>();
                var instanceParameter = Expression.Parameter(typeof(IEventSource), "instance");
                var eventParameter = Expression.Parameter(typeof(object), "@event");
                foreach (var method in 
                    typeOfSource
                        .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                        .Where(candidateMethod => 
                            candidateMethod.Name == "Apply" 
                            && candidateMethod.GetParameters().Length == 1 
                            && candidateMethod.ReturnType == typeof(void)
                        )
                )
                {
                    var typeOfEvent = method.GetParameters()[0].ParameterType;
                    var route = Expression.Lambda<Action<IEventSource, object>>(
                        Expression.Call(
                            Expression.Convert(instanceParameter, typeOfSource), 
                            method, 
                            Expression.Convert(eventParameter, typeOfEvent)),
                        instanceParameter,
                        eventParameter
                    ).Compile();

                    builder.Add(
                        instance => 
                            Tuple.Create<Type, Action<object>>(
                                typeOfEvent, 
                                @event => route(instance, @event))
                    );
                }
                return builder.ToArray();
            });

            Array.ForEach(routeFactories, routeFactory => {
                var route = routeFactory(source);
                register(route.Item1, route.Item2);
            });
        }
    }
}