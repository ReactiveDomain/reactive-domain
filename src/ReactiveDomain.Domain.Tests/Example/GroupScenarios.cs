using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ReactiveDomain;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Example
{
    [Collection(nameof(EmbeddedEventStoreCollection))]
    public class GroupScenarios
    {
        private static readonly Guid CorrelationId = Guid.NewGuid();
        private static readonly Guid SourceId = Guid.NewGuid();

        private readonly ScenarioRunner _runner;

        public GroupScenarios(EmbeddedEventStoreFixture fixture)
        {
            var settings = new JsonSerializerSettings();
            var prefix = fixture.NextStreamNamePrefix();
            var converter = StreamNameConversions.WithPrefix(prefix);
            _runner = new ScenarioRunner(
                new CommandHandlerInvoker(new CommandHandlerModule[]
                {
                    new GroupModule(
                        new GroupRepository(fixture.Connection,
                            new EventSourceReaderConfiguration(
                                converter, 
                                () => new StreamEventsSliceTranslator(
                                    name => Type.GetType(name, true),
                                    settings),
                                new SliceSize(100)),
                            new EventSourceWriterConfiguration(
                                converter, 
                                new EventSourceChangesetTranslator(
                                    type => type.FullName,
                                    settings))))
                }),
                fixture.Connection,
                settings,
                prefix);
        }

        [Fact]
        public Task when_starting_a_group()
        {
            var groupId = new GroupIdentifier(Guid.NewGuid());
            var administratorId = new GroupAdministratorIdentifier(Guid.NewGuid());
            
            return new Scenario()
                .GivenNone()
                .When(new StartGroup(
                    groupId,
                    "Elvis Afficionados",
                    administratorId))
                .Then(groupId,
                    new GroupStarted(
                        groupId,
                        "Elvis Afficionados",
                        administratorId,
                        0
                    )
                )
                .AssertAsync(_runner);
        }

        [Fact]
        public Task when_starting_a_started_group()
        {
            var groupId = new GroupIdentifier(Guid.NewGuid());
            var administratorId = new GroupAdministratorIdentifier(Guid.NewGuid());
            return new Scenario()
                .Given(groupId,
                    new GroupStarted(
                        groupId,
                        "Elvis Afficionados",
                        administratorId,
                        0
                    )
                )
                .When(new StartGroup(
                    groupId,
                    "Elvis Afficionados",
                    administratorId))
                .ThenNone()
                .AssertAsync(_runner);
        }

        [Fact]
        public Task when_stopping_a_started_group()
        {
            var groupId = new GroupIdentifier(Guid.NewGuid());
            var administratorId = new GroupAdministratorIdentifier(Guid.NewGuid());
            return new Scenario()
                .Given(groupId,
                    new GroupStarted(
                        groupId,
                        "Elvis Afficionados",
                        administratorId,
                        0)
                )
                .When(new StopGroup(
                    groupId.ToGuid(),
                    administratorId))
                .Then(groupId,
                    new GroupStopped(
                        groupId,
                        "Elvis Afficionados",
                        administratorId,
                        0)
                )
                .AssertAsync(_runner);
        }

        [Fact]
        public Task when_stopping_a_stopped_group()
        {
            var groupId = new GroupIdentifier(Guid.NewGuid());
            var administratorId = new GroupAdministratorIdentifier(Guid.NewGuid());
            return new Scenario()
                .Given(groupId,
                    new GroupStarted(
                        groupId,
                        "Elvis Afficionados",
                        administratorId,
                        0),
                    new GroupStopped(
                        groupId,
                        "Elvis Afficionados",
                        administratorId,
                        0)
                )
                .When(new StopGroup(groupId, administratorId))
                .ThenNone()
                .AssertAsync(_runner);
        }
    }
}
