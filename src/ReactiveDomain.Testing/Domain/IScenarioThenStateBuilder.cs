using System.Collections.Generic;

namespace ReactiveDomain.Testing
{
    public interface IScenarioThenStateBuilder : IExpectEventsScenarioBuilder
    {
        IScenarioThenStateBuilder Then(IEnumerable<RecordedEvent> events);
    }
}