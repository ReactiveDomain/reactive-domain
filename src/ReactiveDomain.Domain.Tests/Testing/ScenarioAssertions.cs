using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ReactiveDomain.Testing;
using Xunit.Sdk;

namespace ReactiveDomain.Testing
{
    public static class ScenarioAssertions
    {
        public static async Task AssertAsync(
            this IExpectEventsScenarioBuilder builder,
            ScenarioRunner runner,
            CancellationToken ct = default(CancellationToken))
        {
            var scenario = builder.Build();
            var result = await runner.RunAsync(scenario, ct);
            switch (result)
            {
                case ScenarioExpectedEventsButThrewException threw:
                    throw new XunitException($"Expected events but threw {threw.Actual}");
                case ScenarioExpectedEventsButRecordedOtherEvents recorded:
                    var messageBuilder = new StringBuilder();
                    if (recorded.Scenario.Thens.Length != recorded.Actual.Length)
                    {
                        messageBuilder.AppendFormat("Expected {0} events ({1}) but recorded {2} events ({3}).",
                            recorded.Scenario.Thens.Length,
                            string.Join(",",
                                recorded.Scenario.Thens.Select(given => $"{given.Stream} - {given.Event.GetType().Name}")),
                            recorded.Actual.Length,
                            string.Join(",",
                                recorded.Actual.Select(actual => $"{actual.Stream} - {actual.Event.GetType().Name}")));
                    }
                    else
                    {
                        messageBuilder.AppendLine("Expected events to match but found the following differences:");
                        //foreach (var difference in recorded.Differences)
                        //{
                        //    messageBuilder.AppendLine("\t" + difference);
                        //}
                    }
                    throw new XunitException(messageBuilder.ToString());
            }
        }

        public static async Task AssertAsync(
            this IExpectExceptionScenarioBuilder builder,
            ScenarioRunner runner,
            CancellationToken ct = default(CancellationToken))
        {
            var scenario = builder.Build();
            var result = await runner.RunAsync(scenario, ct);
            switch (result)
            {
                case ScenarioExpectedExceptionButThrewNoException _:
                    throw new XunitException("Expected exception but threw no exception");
                case ScenarioExpectedExceptionButThrewOtherException threw:
                    throw new XunitException($"Expected exception but threw {threw.Actual}");
                case ScenarioExpectedExceptionButRecordedEvents recorded:
                    var messageBuilder = new StringBuilder();
                    messageBuilder.AppendLine("Expected exception but recorded these events:");
                    foreach (var recordedEvent in recorded.Actual)
                    {
                        messageBuilder.AppendLine($"\t{recordedEvent.Stream} - {recordedEvent.Event.GetType().Name} - {JsonConvert.SerializeObject(recordedEvent.Event)}");
                    }
                    throw new XunitException(messageBuilder.ToString());
            }
        }
    }
}