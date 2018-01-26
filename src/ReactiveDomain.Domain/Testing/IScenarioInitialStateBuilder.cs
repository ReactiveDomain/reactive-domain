using System.Collections.Generic;

namespace ReactiveDomain.Testing
{
    public interface IScenarioInitialStateBuilder
    {
        IScenarioGivenNoneStateBuilder GivenNone();
        IScenarioGivenStateBuilder Given(IEnumerable<RecordedEvent> events);
        IScenarioWhenStateBuilder When(object command);
    }
}