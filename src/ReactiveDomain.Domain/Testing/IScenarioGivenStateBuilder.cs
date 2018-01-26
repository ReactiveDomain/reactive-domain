using System.Collections.Generic;

namespace ReactiveDomain.Testing
{
    public interface IScenarioGivenStateBuilder
    {
        IScenarioGivenStateBuilder Given(IEnumerable<RecordedEvent> events);
        IScenarioWhenStateBuilder When(object command);
    }
}