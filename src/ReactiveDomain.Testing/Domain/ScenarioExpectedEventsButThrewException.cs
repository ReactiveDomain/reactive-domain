using System;

namespace ReactiveDomain.Testing
{
    public class ScenarioExpectedEventsButThrewException
    {
        public ScenarioExpectedEventsButThrewException(ExpectEventsScenario scenario, Exception actual)
        {
            Scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            Actual = actual ?? throw new ArgumentNullException(nameof(actual));
        }

        public ExpectEventsScenario Scenario { get; }
        public Exception Actual { get; }
    }
}