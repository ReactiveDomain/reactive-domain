using System;
using System.Linq;

namespace ReactiveDomain.Testing
{
    public static class ScenarioExtensions
    {
        public static IScenarioGivenStateBuilder Given(
            this IScenarioInitialStateBuilder builder,
            StreamName stream,
            params object[] events)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            return builder.Given(events.Select(@event => new RecordedEvent(stream, @event)));
        }

        public static IScenarioGivenStateBuilder Given(
            this IScenarioGivenStateBuilder builder, 
            StreamName stream,
            params object[] events)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            return builder.Given(events.Select(@event => new RecordedEvent(stream, @event)));
        }

        public static IScenarioThenStateBuilder Then(
            this IScenarioWhenStateBuilder builder,
            StreamName stream,
            params object[] events)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            return builder.Then(events.Select(@event => new RecordedEvent(stream, @event)));
        }

        public static IScenarioThenStateBuilder Then(
            this IScenarioThenStateBuilder builder,
            StreamName stream,
            params object[] events)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            return builder.Then(events.Select(@event => new RecordedEvent(stream, @event)));
        }
    }
}