using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ReactiveDomain;
using Xunit;

namespace ReactiveDomain
{
    [Collection(nameof(EmbeddedEventStoreCollection))]
    public class EventSourceReaderWriterTests
    {
        private readonly EmbeddedEventStoreFixture _fixture;

        public EventSourceReaderWriterTests(EmbeddedEventStoreFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task CanReadUsingReaderWhatWasWrittenUsingWriter()
        {
            var stream = _fixture.NextStreamName();

            var random = new Random();
            var value1 = random.Next();
            var value2 = random.Next();

            // write
            var changes = new object[]
            {
                new Event1 { Value = value1 },
                new Event2 { Value = value2 }
            };
            var writable = new Entity(changes);
            var writer = new EventSourceWriter(
                _fixture.Connection,
                new EventSourceWriterConfiguration(
                    StreamNameConversions.PassThru, 
                    new EventSourceChangesetTranslator(type => type.FullName, new JsonSerializerSettings())));
            await writer.WriteStreamAsync(stream, writable, Guid.NewGuid(), Guid.NewGuid());

            //read
            var reader = new EventSourceReader(
                () => new Entity(),
                _fixture.Connection,
                new EventSourceReaderConfiguration(
                    StreamNameConversions.PassThru, 
                    () => new StreamEventsSliceTranslator(
                        name => Type.GetType(name, true),
                        new JsonSerializerSettings()),
                    new SliceSize(1)));
            var result = await reader.ReadStreamAsync(stream);

            Assert.Equal(ReadResultState.Found, result.State);
            var readable = Assert.IsAssignableFrom<Entity>(result.Value);
            Assert.Equal(value1, readable.Value1);
            Assert.Equal(value2, readable.Value2);
        }

        class Event1
        {
            public int Value { get; set; }
        }

        class Event2
        {
            public int Value { get; set; }
        }

        class Entity : AggregateRootEntity
        {
            public Entity(params object[] changes)
            {
                Register<Event1>(_ =>
                {
                    Value1 = _.Value;
                });

                Register<Event2>(_ =>
                {
                    Value2 = _.Value;
                });

                foreach (var change in changes)
                    Raise(change);
            }

            public int Value1 { get; private set; }
            public int Value2 { get; private set; }
        }
    }
}
