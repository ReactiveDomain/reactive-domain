using System;

namespace ReactiveDomain.Testing
{
    public class ExpectEventsScenarioPassed
    {
        public ExpectEventsScenarioPassed(ExpectEventsScenario scenario)
        {
            Scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
        }

        public ExpectEventsScenario Scenario { get; }
    }
}