using System;
using System.Collections.Generic;

namespace ReactiveDomain.Testing
{
    public interface IScenarioWhenStateBuilder
    {
        IScenarioThenNoneStateBuilder ThenNone();
        IScenarioThenStateBuilder Then(IEnumerable<RecordedEvent> events);
        IScenarioThrowsStateBuilder Throws(Exception exception);
    }
}