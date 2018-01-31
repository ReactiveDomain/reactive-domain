using System;

namespace ReactiveDomain.Testing
{
    public class ScenarioExpectedExceptionButThrewOtherException
    {
        public ScenarioExpectedExceptionButThrewOtherException(ExpectExceptionScenario scenario, Exception actual)
        {
            Scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            Actual = actual ?? throw new ArgumentNullException(nameof(actual));
        }

        public ExpectExceptionScenario Scenario { get; }
        public Exception Actual { get; }
    }
}