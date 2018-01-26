using System;

namespace ReactiveDomain.Testing
{
    public class ScenarioExpectedExceptionButRecordedEvents
    {
        public ScenarioExpectedExceptionButRecordedEvents(ExpectExceptionScenario scenario, RecordedEvent[] actual)
        {
            Scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            Actual = actual ?? throw new ArgumentNullException(nameof(actual));
        }

        public ExpectExceptionScenario Scenario { get; }
        public RecordedEvent[] Actual { get; }
    }
}