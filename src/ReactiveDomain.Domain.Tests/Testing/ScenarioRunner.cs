using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using KellermanSoftware.CompareNetObjects;
using Newtonsoft.Json;
using ReactiveDomain.Testing;

namespace ReactiveDomain.Domain.Tests.Testing
{
    public class ScenarioRunner
    {
        private readonly CommandHandlerInvoker _invoker;
        private readonly IEventStoreConnection _connection;
        private readonly JsonSerializerSettings _settings;
        private readonly string _prefix;
        private readonly StreamNameConverter _converter;

        public ScenarioRunner(CommandHandlerInvoker invoker, IEventStoreConnection connection, JsonSerializerSettings settings, string prefix)
        {
            _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _prefix = prefix ?? throw new ArgumentNullException(nameof(prefix));
            _converter = StreamNameConversions.WithPrefix(prefix);
        }

        public async Task<object> RunAsync(ExpectEventsScenario scenario, CancellationToken ct = default(CancellationToken))
        {
            var checkpoint = await WriteGivens(scenario.Givens);
            var envelope = scenario.When is CommandEnvelope
                ? (CommandEnvelope) scenario.When
                : new CommandEnvelope()
                    .SetCommand(scenario.When)
                    .SetCommandId(Guid.NewGuid())
                    .SetCorrelationId(Guid.NewGuid())
                    .SetSourceId(Guid.NewGuid());
            var exception = await Catch.Exception(() => _invoker.Invoke(envelope, ct));
            if (exception != null)
            {
                return scenario.ButThrewException(exception);
            }

            var recordedEvents = await ReadThens(checkpoint);
            if (scenario.Givens.Length != 0 && recordedEvents.Length != 0)
            {
                recordedEvents = recordedEvents.Skip(1).ToArray();
            }
            var config = new ComparisonConfig
            {
                MaxDifferences = int.MaxValue,
                MaxStructDepth = 5,
                MembersToIgnore = new List<string>
                {
                    "MsgId"
                }
            };
            var comparer = new CompareLogic(config);
            var expectedEvents = Array.ConvertAll(scenario.Thens,
                then => new ReactiveDomain.Testing.RecordedEvent(_converter(new StreamName(then.Stream)), then.Event));
            var result = comparer.Compare(expectedEvents, recordedEvents);
            if (result.AreEqual) return scenario.Pass();
            return scenario.ButRecordedOtherEvents(recordedEvents); //, result.Differences.ToArray()
        }

        public async Task<object> RunAsync(ExpectExceptionScenario scenario, CancellationToken ct = default(CancellationToken))
        {
            var checkpoint = await WriteGivens(scenario.Givens);

            var envelope = scenario.When is CommandEnvelope
                ? (CommandEnvelope)scenario.When
                : new CommandEnvelope()
                    .SetCommand(scenario.When)
                    .SetCommandId(Guid.NewGuid())
                    .SetCorrelationId(Guid.NewGuid())
                    .SetSourceId(Guid.NewGuid());
            var exception = await Catch.Exception(() => _invoker.Invoke(envelope, ct));
            if (exception == null)
            {
                var recordedEvents = await ReadThens(checkpoint);
                if (scenario.Givens.Length != 0 && recordedEvents.Length != 0)
                {
                    recordedEvents = recordedEvents.Skip(1).ToArray();
                }
                if (recordedEvents.Length != 0)
                {
                    return scenario.ButRecordedEvents(recordedEvents);
                }
                return scenario.ButThrewNoException();
            }

            var config = new ComparisonConfig
            {
                MaxDifferences = int.MaxValue,
                MaxStructDepth = 5,
                MembersToIgnore =
                {
                    "StackTrace",
                    "Source",
                    "TargetSite"
                }
            };
            var comparer = new CompareLogic(config);
            var result = comparer.Compare(scenario.Throws, exception);
            if (result.AreEqual) return scenario.Pass();
            return scenario.ButThrewException(exception);
        }

        private async Task<Position> WriteGivens(ReactiveDomain.Testing.RecordedEvent[] givens)
        {
            var checkpoint = Position.Start;
            foreach (var stream in givens.GroupBy(given => given.Stream))
            {
                var result = await _connection.AppendToStreamAsync(
                    _converter(new StreamName(stream.Key)),
                    ExpectedVersion.NoStream,
                    stream.Select(given => new EventData(
                        Guid.NewGuid(),
                        given.Event.GetType().FullName,
                        true,
                        Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(given.Event, _settings)),
                        new byte[0]
                    )));
                checkpoint = result.LogPosition;
            }
            return checkpoint;
        }

        private async Task<ReactiveDomain.Testing.RecordedEvent[]> ReadThens(Position position)
        {
            var recorded = new List<ReactiveDomain.Testing.RecordedEvent>();
            var slice = await _connection.ReadAllEventsForwardAsync(position, 1024, false, new UserCredentials("admin", "changeit"));
            recorded.AddRange(
                slice.Events
                    .Where(resolved => 
                        !resolved.OriginalStreamId.StartsWith("$") 
                        && resolved.OriginalStreamId.StartsWith(_prefix.ToString())
                        && resolved.OriginalEvent.IsJson)
                    .Select(resolved => new ReactiveDomain.Testing.RecordedEvent(
                        new StreamName(resolved.OriginalStreamId),
                        JsonConvert.DeserializeObject(
                            Encoding.UTF8.GetString(resolved.OriginalEvent.Data),
                            Type.GetType(resolved.OriginalEvent.EventType, true),
                            _settings))));
            while (!slice.IsEndOfStream)
            {
                slice = await _connection.ReadAllEventsForwardAsync(slice.NextPosition, 1024, false, new UserCredentials("admin", "changeit"));
                recorded.AddRange(
                    slice.Events
                        .Where(resolved =>
                            !resolved.OriginalStreamId.StartsWith("$")
                            && resolved.OriginalStreamId.StartsWith(_prefix.ToString())
                            && resolved.OriginalEvent.IsJson)
                        .Select(resolved => new ReactiveDomain.Testing.RecordedEvent(
                            new StreamName(resolved.OriginalStreamId),
                            JsonConvert.DeserializeObject(
                                Encoding.UTF8.GetString(resolved.OriginalEvent.Data),
                                Type.GetType(resolved.OriginalEvent.EventType, true),
                                _settings))));
            }
            return recorded.ToArray();
        }
    }
}
