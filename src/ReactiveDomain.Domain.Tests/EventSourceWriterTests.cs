using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using Newtonsoft.Json;
using Xunit;

namespace ReactiveDomain
{
    [Collection(nameof(EmbeddedEventStoreCollection))]
    public class EventSourceWriterTests
    {
        private readonly EmbeddedEventStoreFixture _fixture;

        public EventSourceWriterTests(EmbeddedEventStoreFixture fixture)
        {
            _fixture = fixture;
        }

        // Null related tests

        [Fact]
        public void ConnectionCanNotBeNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new EventSourceWriter(
                    null,
                    new EventSourceWriterConfiguration(StreamNameConversions.PassThru, 
                        new EventSourceChangesetTranslator(type => type.Name, new JsonSerializerSettings()))
                ));
        }

        [Fact]
        public void ConfigurationCanNotBeNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new EventSourceWriter(
                    _fixture.Connection,
                    null
                ));
        }

        [Fact]
        public Task WriteStreamAsyncSourceCanNotBeNull()
        {
            var sut =
                new EventSourceWriter(
                    _fixture.Connection,
                    new EventSourceWriterConfiguration(StreamNameConversions.PassThru, 
                        new EventSourceChangesetTranslator(type => type.Name, new JsonSerializerSettings()))
                );

            return
                Assert.ThrowsAsync<ArgumentNullException>(() =>
                    sut.WriteStreamAsync(_fixture.NextStreamName(), null, Guid.Empty, Guid.Empty));
        }

        // Integration tests (Please note that "result.Position" can't be asserted - affected by the stream writes that happen in the store)

        // No stream cases

        [Fact]
        public async Task WhenNoStreamAndNoChangesReturnsExpectedResult()
        {
            var stream = _fixture.NextStreamName();
            var eventSource = new Entity();

            var sut = CreateSut();
            
            var result = await sut.WriteStreamAsync(stream, eventSource, Guid.NewGuid(), Guid.NewGuid());

            Assert.Equal(ExpectedVersion.NoStream, result.NextExpectedVersion);
            Assert.Equal(ExpectedVersion.NoStream, eventSource.RevealExpectedVersion);
        }

        [Fact]
        public async Task WhenNoStreamAndChangesReturnsExpectedResult()
        {
            //Arrange
            var stream = _fixture.NextStreamName();
            var changes = new object[]
            {
                new Event1 {Value = 1},
                new Event1 {Value = 2}
            };
            var eventSource = new Entity(changes);

            var sut = CreateSut();

            //Act
            var result = await sut.WriteStreamAsync(stream, eventSource, Guid.NewGuid(), Guid.NewGuid());

            Assert.Equal(1, result.NextExpectedVersion);
            Assert.Equal(1, eventSource.RevealExpectedVersion);
            
        }

        [Fact]
        public async Task WhenNoStreamAndChangesMismatchOnCommandIdThenThrows()
        {
            //Arrange
            var stream = _fixture.NextStreamName();
            var sut = CreateSut();
            var command1 = Guid.NewGuid();
            var changes = new object[]
            {
                new Event1 {Value = 1},
                new Event1 {Value = 2}
            };
            var eventSource1 = new Entity(changes);
            await sut.WriteStreamAsync(stream, eventSource1, command1, Guid.NewGuid());

            var command2 = Guid.NewGuid();
            var eventSource2 = new Entity(changes);

            //Act
            await Assert.ThrowsAsync<WrongExpectedVersionException>(
                () => sut.WriteStreamAsync(stream, eventSource2, command2, Guid.NewGuid()));
        }

        [Fact]
        public async Task WhenNoStreamAndChangesMismatchOnEventTypeThenThrows()
        {
            //Arrange
            var stream = _fixture.NextStreamName();
            var sut = CreateSut();
            var command = Guid.NewGuid();
            var changes1 = new object[]
            {
                new Event1 {Value = 1},
                new Event1 {Value = 2}
            };
            var eventSource1 = new Entity(changes1);
            await sut.WriteStreamAsync(stream, eventSource1, command, Guid.NewGuid());

            var changes2 = new object[]
            {
                new Event2 {Value = 1},
                new Event2 {Value = 2}
            };
            var eventSource2 = new Entity(changes2);

            //Act
            await Assert.ThrowsAsync<WrongExpectedVersionException>(
                () => sut.WriteStreamAsync(stream, eventSource2, command, Guid.NewGuid()));
        }

        [Fact]
        public async Task WhenNoStreamAndChangesMismatchOnEventTypeOrderThenThrows()
        {
            //Arrange
            var stream = _fixture.NextStreamName();
            var sut = CreateSut();
            var command = Guid.NewGuid();
            var changes1 = new object[]
            {
                new Event1 {Value = 1},
                new Event2 {Value = 2}
            };
            var eventSource1 = new Entity(changes1);
            await sut.WriteStreamAsync(stream, eventSource1, command, Guid.NewGuid());

            var changes2 = new object[]
            {
                new Event2 {Value = 1},
                new Event1 {Value = 2}
            };
            var eventSource2 = new Entity(changes2);

            //Act
            await Assert.ThrowsAsync<WrongExpectedVersionException>(
                () => sut.WriteStreamAsync(stream, eventSource2, command, Guid.NewGuid()));
        }

        [Fact]
        public async Task WhenNoStreamAndChangesMismatchOnNumberOfEventsThenThrows()
        {
            //Arrange
            var stream = _fixture.NextStreamName();
            var sut = CreateSut();
            var command = Guid.NewGuid();
            var changes1 = new object[]
            {
                new Event1 {Value = 1},
                new Event1 {Value = 2}
            };
            var eventSource1 = new Entity(changes1);
            await sut.WriteStreamAsync(stream, eventSource1, command, Guid.NewGuid());

            var changes2 = new object[]
            {
                new Event1 {Value = 1},
                new Event1 {Value = 2},
                new Event1 {Value = 3}
            };
            var eventSource2 = new Entity(changes2);

            //Act
            await Assert.ThrowsAsync<WrongExpectedVersionException>(
                () => sut.WriteStreamAsync(stream, eventSource2, command, Guid.NewGuid()));
        }

        // Existing stream cases

        [Fact]
        public async Task WhenStreamPresentAndNoChangesReturnsExpectedResult()
        {
            //Arrange
            var stream = _fixture.NextStreamName();
            var initial = await CreateStream(stream, CreateEventData());
            var eventSource = new Entity(initial.NextExpectedVersion, new object[0]);
            var sut = CreateSut();

            //Act
            var result = await sut.WriteStreamAsync(stream, eventSource, Guid.NewGuid(), Guid.NewGuid());

            Assert.Equal(initial.NextExpectedVersion, result.NextExpectedVersion);
            Assert.Equal(initial.NextExpectedVersion, eventSource.RevealExpectedVersion);
        }

        [Fact]
        public async Task WhenStreamPresentAndChangesReturnsExpectedResult()
        {
            //Arrange
            var stream = _fixture.NextStreamName();
            var initial = await CreateStream(stream, CreateEventData());
            var changes = new object[]
            {
                new Event1 {Value = 1},
                new Event1 {Value = 2}
            };
            var eventSource = new Entity(initial.NextExpectedVersion, changes);
            var sut = CreateSut();

            //Act
            var result = await sut.WriteStreamAsync(stream, eventSource, Guid.NewGuid(), Guid.NewGuid());

            Assert.Equal(initial.NextExpectedVersion + 2, result.NextExpectedVersion);
            Assert.Equal(initial.NextExpectedVersion + 2, eventSource.RevealExpectedVersion);
        }

        [Fact]
        public async Task WhenStreamPresentAndChangesMismatchOnCommandIdThenThrows()
        {
            //Arrange
            var stream = _fixture.NextStreamName();
            var initial = await CreateStream(stream, CreateEventData());
            var sut = CreateSut();
            var command1 = Guid.NewGuid();
            var changes = new object[]
            {
                new Event1 {Value = 1},
                new Event1 {Value = 2}
            };
            var eventSource1 = new Entity(initial.NextExpectedVersion, changes);
            await sut.WriteStreamAsync(stream, eventSource1, command1, Guid.NewGuid());

            var command2 = Guid.NewGuid();
            var eventSource2 = new Entity(initial.NextExpectedVersion, changes);

            //Act
            await Assert.ThrowsAsync<WrongExpectedVersionException>(
                () => sut.WriteStreamAsync(stream, eventSource2, command2, Guid.NewGuid()));
        }

        [Fact]
        public async Task WhenStreamPresentAndChangesMismatchOnEventTypeThenThrows()
        {
            //Arrange
            var stream = _fixture.NextStreamName();
            var initial = await CreateStream(stream, CreateEventData());
            var sut = CreateSut();
            var command = Guid.NewGuid();
            var changes1 = new object[]
            {
                new Event1 {Value = 1},
                new Event1 {Value = 2}
            };
            var eventSource1 = new Entity(initial.NextExpectedVersion, changes1);
            await sut.WriteStreamAsync(stream, eventSource1, command, Guid.NewGuid());

            var changes2 = new object[]
            {
                new Event2 {Value = 1},
                new Event2 {Value = 2}
            };
            var eventSource2 = new Entity(initial.NextExpectedVersion, changes2, Guid.NewGuid());

            //Act
            await Assert.ThrowsAsync<WrongExpectedVersionException>(
                () => sut.WriteStreamAsync(stream, eventSource2, command, Guid.NewGuid()));
        }

        [Fact]
        public async Task WhenStreamPresentAndChangesMismatchOnEventTypeOrderThenThrows()
        {
            //Arrange
            var stream = _fixture.NextStreamName();
            var initial = await CreateStream(stream, CreateEventData());
            var sut = CreateSut();
            var command = Guid.NewGuid();
            var changes1 = new object[]
            {
                new Event1 {Value = 1},
                new Event2 {Value = 2}
            };
            var eventSource1 = new Entity(initial.NextExpectedVersion, changes1);
            //throw new Exception(((IEventSource)eventSource1).ExpectedVersion.ToString());
            await sut.WriteStreamAsync(stream, eventSource1, command, Guid.NewGuid());

            var changes2 = new object[]
            {
                new Event2 {Value = 1},
                new Event1 {Value = 2}
            };
            var eventSource2 = new Entity(0, changes2);

            //Act
            await Assert.ThrowsAsync<WrongExpectedVersionException>(
                () => sut.WriteStreamAsync(stream, eventSource2, command, Guid.NewGuid()));
        }

        [Fact]
        public async Task WhenStreamPresentAndChangesMismatchOnNumberOfEventsThenThrows()
        {
            //Arrange
            var stream = _fixture.NextStreamName();
            var initial = await CreateStream(stream, CreateEventData());
            var sut = CreateSut();
            var command = Guid.NewGuid();
            var changes1 = new object[]
            {
                new Event1 {Value = 1},
                new Event1 {Value = 2}
            };
            var eventSource1 = new Entity(initial.NextExpectedVersion, changes1);
            await sut.WriteStreamAsync(stream, eventSource1, command, Guid.NewGuid());

            var changes2 = new object[]
            {
                new Event1 {Value = 1},
                new Event1 {Value = 2},
                new Event1 {Value = 3}
            };
            var eventSource2 = new Entity(initial.NextExpectedVersion, changes2);

            //Act
            await Assert.ThrowsAsync<WrongExpectedVersionException>(
                () => sut.WriteStreamAsync(stream, eventSource2, command, Guid.NewGuid()));
        }

        [Fact]
        public async Task WhenStreamPresentAndChangesMismatchExpectedVersionThenThrows()
        {
            //Arrange
            var stream = _fixture.NextStreamName();
            var initial = await CreateStream(stream, CreateEventData());
            var sut = CreateSut();
            var command = Guid.NewGuid();
            var changes1 = new object[]
            {
                new Event1 {Value = 1},
                new Event1 {Value = 2}
            };
            var eventSource1 = new Entity(initial.NextExpectedVersion, changes1);
            await sut.WriteStreamAsync(stream, eventSource1, command, Guid.NewGuid());

            var changes2 = new object[]
            {
                new Event1 {Value = 1},
                new Event1 {Value = 2}
            };
            var eventSource2 = new Entity(initial.NextExpectedVersion + 1, changes2);

            //Act
            await Assert.ThrowsAsync<WrongExpectedVersionException>(
                () => sut.WriteStreamAsync(stream, eventSource2, command, Guid.NewGuid()));
        }

        // Deleted stream cases

        [Fact] //Not sure what to expect
        public async Task WhenStreamDeletedAndNoChangesReturnsExpectedResult()
        {
            //Arrange
            var stream = _fixture.NextStreamName();
            var initial = await CreateStream(stream, CreateEventData());
            await DeleteStream(stream);
            var eventSource = new Entity(initial.NextExpectedVersion);
            var sut = CreateSut();

            //Act
            var result = await sut.WriteStreamAsync(stream, eventSource, Guid.NewGuid(), Guid.NewGuid());

            Assert.Equal(initial.NextExpectedVersion, result.NextExpectedVersion);
            Assert.Equal(initial.NextExpectedVersion, eventSource.RevealExpectedVersion);            
        }

        [Fact] // Expected a deleted stream not to be writable.
        public async Task WhenStreamDeletedAndChangesReturnsExpectedResult()
        {
            //Arrange
            var stream = _fixture.NextStreamName();
            var initial = await CreateStream(stream, CreateEventData());
            await DeleteStream(stream);
            var changes = new object[]
            {
                new Event1 {Value = 1},
                new Event1 {Value = 2}
            };
            var eventSource = new Entity(initial.NextExpectedVersion, changes);
            var sut = CreateSut();

            //Act
            var result = await sut.WriteStreamAsync(stream, eventSource, Guid.NewGuid(), Guid.NewGuid());

            Assert.Equal(initial.NextExpectedVersion + 2, result.NextExpectedVersion);
            Assert.Equal(initial.NextExpectedVersion + 2, eventSource.RevealExpectedVersion);
        }

        // Cancellation

        [Fact]
        public async Task WhenCancelledBeforeWriteThrowsExpectedException()
        {
            var stream = _fixture.NextStreamName();
            await CreateStream(stream, CreateEventData());

            var source = new CancellationTokenSource();
            var eventSource = new Entity();
            var sut = CreateSut();

            source.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                sut.WriteStreamAsync(stream, eventSource, Guid.NewGuid(), Guid.NewGuid(), null, source.Token));
        }

        [Fact]
        public async Task WhenCancelledMidWriteThrowsExpectedException()
        {
            var stream = _fixture.NextStreamName();
            await CreateStream(stream, CreateEventData());

            var source = new CancellationTokenSource();
            var eventSource = new Entity(() =>
            {
                source.Cancel(); //mimics mid write, poorly
            }, new Event1 { Value = 1 });
            var sut = CreateSut();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                sut.WriteStreamAsync(stream, eventSource, Guid.NewGuid(), Guid.NewGuid(), null, source.Token));
        }

        class Event1
        {
            public int Value { get; set; }
        }

        class Event2
        {
            public int Value { get; set; }
        }

        private EventSourceWriter CreateSut()
        {
            return new EventSourceWriter(
                _fixture.Connection,
                new EventSourceWriterConfiguration(StreamNameConversions.PassThru, 
                    new EventSourceChangesetTranslator(type => type.FullName, new JsonSerializerSettings()))
            );
        }

        private Task<WriteResult> CreateStream(string stream, params EventData[] events)
        {
            return _fixture.Connection.AppendToStreamAsync(stream, ExpectedVersion.NoStream, events);
        }

        private Task<DeleteResult> DeleteStream(string stream)
        {
            return _fixture.Connection.DeleteStreamAsync(
                stream,
                ExpectedVersion.Any);
        }

        private static EventData CreateEventData()
        {
            return new EventData(Guid.NewGuid(), "-", true, Encoding.UTF8.GetBytes("{}"), Encoding.UTF8.GetBytes("{}"));
        }

        class Entity : AggregateRootEntity
        {
            public Entity(params object[] changes)
            {
                foreach(var change in changes)
                    Raise(change);
            }

            public Entity(long expectedVersion, params object[] changes)
            {
                foreach (var change in changes)
                    Raise(change);

                ((IEventSource)this).ExpectedVersion = expectedVersion;
            }

            public Entity(Action onEvent, params object[] changes)
            {
                Register<Event1>(_ =>
                {
                    onEvent();
                });
                Register<Event2>(_ =>
                {
                    onEvent();
                });

                foreach (var change in changes)
                    Raise(change);
            }

            public long RevealExpectedVersion => ((IEventSource)this).ExpectedVersion;
        }
    }
}