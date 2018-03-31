using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Domain.Tests
{
    [Collection(nameof(EmbeddedStreamStoreConnectionCollection))]
    public class EventSourceReaderTests
    {
        private readonly StreamStoreConnectionFixture _fixture;

        public EventSourceReaderTests(StreamStoreConnectionFixture fixture)
        {
            _fixture = fixture;
        }

        // Null related tests

        [Fact]
        public void FactoryCanNotBeNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new EventSourceReader(
                    null,
                    _fixture.Connection,
                    new EventSourceReaderConfiguration(StreamNameConversions.PassThru, () => null, new SliceSize(1))
                ));
        }

        [Fact]
        public void ConnectionCanNotBeNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new EventSourceReader(
                    () => null, 
                    null,
                    new EventSourceReaderConfiguration(StreamNameConversions.PassThru, () => null, new SliceSize(1))
                ));
        }

        [Fact]
        public void ConfigurationCanNotBeNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new EventSourceReader(
                    () => null,
                    _fixture.Connection,
                    null
                ));
        }

        // Integration tests

        [Fact]
        public async Task WhenNoStreamReturnsExpectedResult()
        {
            var stream = _fixture.NextStreamName();
            var sut = CreateSut();
            
            var result = await sut.ReadStreamAsync(stream, CancellationToken.None);

            Assert.Equal(ReadResult.NotFound, result);
        }

        [Fact]
        public async Task WhenStreamPresentReturnsExpectedResult()
        {
            var stream = _fixture.NextStreamName();
            var eventData1 = CreateEventData();
            var eventData2 = CreateEventData();
            await CreateStream(stream, eventData1, eventData2);

            var sut = CreateSut();

            var result = await sut.ReadStreamAsync(stream, CancellationToken.None);

            Assert.Equal(ReadResultState.Found, result.State);
            var found = (Entity) result.Value;
            Assert.Equal(2, found.Route.Captured.Length);
            var localEvent1 = Assert.IsAssignableFrom<LocalEvent>(found.Route.Captured[0]);
            var resolvedEvent1 = localEvent1.ResolvedEvent;
            Assert.Equal(eventData1.EventId, resolvedEvent1.OriginalEvent.EventId);
            Assert.Equal(eventData1.Type, resolvedEvent1.OriginalEvent.EventType);
            Assert.Equal(eventData1.Data, resolvedEvent1.OriginalEvent.Data);
            Assert.Equal(eventData1.IsJson, resolvedEvent1.OriginalEvent.IsJson);
            Assert.Equal(eventData1.Metadata, resolvedEvent1.OriginalEvent.Metadata);
            var localEvent2 = Assert.IsAssignableFrom<LocalEvent>(found.Route.Captured[1]);
            var resolvedEvent2 = localEvent2.ResolvedEvent;
            Assert.Equal(eventData2.EventId, resolvedEvent2.OriginalEvent.EventId);
            Assert.Equal(eventData2.Type, resolvedEvent2.OriginalEvent.EventType);
            Assert.Equal(eventData2.Data, resolvedEvent2.OriginalEvent.Data);
            Assert.Equal(eventData2.IsJson, resolvedEvent2.OriginalEvent.IsJson);
            Assert.Equal(eventData2.Metadata, resolvedEvent2.OriginalEvent.Metadata);
        }

        [Fact]
        public async Task WhenStreamDeletedReturnsExpectedResult()
        {
            var stream = _fixture.NextStreamName();
            await CreateStream(stream, CreateEventData());
            await DeleteStream(stream);

            var sut = CreateSut();

            var result = await sut.ReadStreamAsync(stream, CancellationToken.None);

            //I expected this to be Deleted but the underlying EmbeddedEventStore says differently
            Assert.Equal(ReadResult.NotFound, result);
        }

        [Fact]
        public async Task WhenStreamDeletedMidReadReturnsExpectedResult()
        {
            var stream = _fixture.NextStreamName();
            await CreateStream(stream, CreateEventData(), CreateEventData());

            var sut = CreateSut(() => new Entity(() =>
            {
                Task.Run(() => _fixture.Connection.DeleteStreamAsync(stream, ExpectedVersion.Any)).Wait();
            }));

            var result = await sut.ReadStreamAsync(stream, CancellationToken.None);

            //I expected this to be Deleted but the underlying EmbeddedEventStore says differently
            Assert.Equal(ReadResult.NotFound, result);
        }

        [Fact]
        public async Task WhenCancelledBeforeReadThrowsExpectedException()
        {
            var stream = _fixture.NextStreamName();
            await CreateStream(stream, CreateEventData());

            var source = new CancellationTokenSource();
            var sut = CreateSut();

            source.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => 
                sut.ReadStreamAsync(stream, source.Token));
        }

        [Fact]
        public async Task WhenCancelledMidReadThrowsExpectedException()
        {
            var stream = _fixture.NextStreamName();
            await CreateStream(stream, CreateEventData(), CreateEventData());

            var source = new CancellationTokenSource();
            var sut = CreateSut(() => new Entity(() =>
            {
                source.Cancel();
            }));

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                sut.ReadStreamAsync(stream, source.Token));
        }

        private EventSourceReader CreateSut(Func<IEventSource> factory = null)
        {
            return new EventSourceReader(
                factory ?? Entity.Factory,
                _fixture.Connection,
                new EventSourceReaderConfiguration(
                    StreamNameConversions.PassThru, 
                    () => PassThruTranslator.Instance, 
                    new SliceSize(1)));
        }

        private Task CreateStream(string stream, params EventData[] events)
        {
            return _fixture.Connection.AppendToStreamAsync(
                stream,
                ExpectedVersion.NoStream,
                events);
        }

        private Task DeleteStream(string stream)
        {
            return _fixture.Connection.DeleteStreamAsync(
                new StreamName(stream),
                ExpectedVersion.Any);
        }

        private static EventData CreateEventData()
        {
            return new EventData(Guid.NewGuid(), "event", true, Encoding.UTF8.GetBytes("{}"), new byte[0]);
        }

        class Entity : AggregateRootEntity
        {
            public static readonly Func<Entity> Factory = () => new Entity(() => { });

            public readonly CapturingManyRoute Route;

            public Entity(Action onEvent)
            {
                Route = new CapturingManyRoute();

                Register<LocalEvent>(_ =>
                {
                    Route.Capture(_);
                    onEvent();
                });
            }
        }

        class LocalEvent
        {
            public ResolvedEvent ResolvedEvent { get; }

            public LocalEvent(ResolvedEvent @event)
            {
                ResolvedEvent = @event;
            }
        }

        class PassThruTranslator : IStreamEventsSliceTranslator
        {
            public static readonly IStreamEventsSliceTranslator Instance = new PassThruTranslator();

            public IEnumerable<object> Translate(StreamEventsSlice slice)
            {
                return slice.Events.Select(_ => new LocalEvent(_));
            }
        }
    }
}
