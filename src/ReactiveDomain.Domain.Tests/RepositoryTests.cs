using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using Newtonsoft.Json;
using Xunit;

namespace ReactiveDomain
{
    [Collection(nameof(EmbeddedEventStoreCollection))]
    public class RepositoryTests
    {
        private class Root : AggregateRootEntity
        {
            public string Token { get; private set; }

            public static readonly Func<Root> Factory = () => new Root();

            private Root()
            {
                Register<Event>(e => Token = e.Token);
            }

            public void Behavior(string token)
            {
                Raise(new Event { Token = token });
            }
        }

        private class Event { public string Token { get; set; } }
        
        private readonly EmbeddedEventStoreFixture _fixture;
        private readonly string _prefix;
        private readonly EventSourceReaderConfiguration _readerConfiguration;
        private readonly EventSourceWriterConfiguration _writerConfiguration;

        public RepositoryTests(EmbeddedEventStoreFixture fixture)
        {
            _fixture = fixture;
            _prefix = _fixture.NextStreamNamePrefix();
            _readerConfiguration = new EventSourceReaderConfiguration(
                StreamNameConversions.WithPrefix(_prefix),
                () => new StreamEventsSliceTranslator(name => typeof(Event), new JsonSerializerSettings()),
                new SliceSize(1)
            );
            _writerConfiguration = new EventSourceWriterConfiguration(
                StreamNameConversions.WithPrefix(_prefix),
                new EventSourceChangesetTranslator(type => "Event", new JsonSerializerSettings())
            );
        }

        [Fact]
        public void IsRepository()
        {
            var sut = CreateSut();

            Assert.IsAssignableFrom<IRepository<Root>>(sut);
        }

        private Repository<Root> CreateSut()
        {
            return new Repository<Root>(
                            Root.Factory,
                            _fixture.Connection,
                            _readerConfiguration,
                            _writerConfiguration);
        }

        [Fact]
        public void FactoryCanNotBeNull()
        {
            Assert.Throws<ArgumentNullException>(() => new Repository<Root>(
                null,
                _fixture.Connection,
                _readerConfiguration,
                _writerConfiguration
            ));
        }

        [Fact]
        public void ConnectionCanNotBeNull()
        {
            Assert.Throws<ArgumentNullException>(() => new Repository<Root>(
                Root.Factory,
                null,
                _readerConfiguration,
                _writerConfiguration
            ));
        }

        [Fact]
        public void ReaderConfigurationCanNotBeNull()
        {
            Assert.Throws<ArgumentNullException>(() => new Repository<Root>(
                Root.Factory,
                _fixture.Connection,
                null,
                _writerConfiguration
            ));
        }

        [Fact]
        public void WriterConfigurationCanNotBeNull()
        {
            Assert.Throws<ArgumentNullException>(() => new Repository<Root>(
                Root.Factory,
                _fixture.Connection,
                _readerConfiguration,
                null
            ));
        }

        [Fact]
        public Task LoadAsyncThrowsWhenNotFound()
        {
            var sut = CreateSut();

            return Assert.ThrowsAsync<StreamNotFoundException>(
                async () => await sut.LoadAsync(new StreamName(Guid.NewGuid().ToString()))
            );
        }

        [Fact]
        public async Task LoadAsyncReturnsExpectedResultWhenFound()
        {
            var stream = new StreamName(Guid.NewGuid().ToString());
            var token = Guid.NewGuid().ToString();
            var writer = new EventSourceWriter(_fixture.Connection, _writerConfiguration);
            var root = Root.Factory();
            root.Behavior(token);
            await writer.WriteStreamAsync(stream, root, Guid.NewGuid(), Guid.NewGuid());

            var sut = CreateSut();

            var result = await sut.LoadAsync(stream);

            Assert.Equal(token, result.Token);            
        }

        [Fact]
        public async Task TryLoadAsyncReturnsExpectedResultWhenNotFound()
        {
            var sut = CreateSut();

            var result = await sut.TryLoadAsync(new StreamName(Guid.NewGuid().ToString()));
            
            Assert.Null(result);
        }

        [Fact]
        public async Task TryLoadAsyncReturnsExpectedResultWhenFound()
        {
            var stream = new StreamName(Guid.NewGuid().ToString());
            var token = Guid.NewGuid().ToString();
            var writer = new EventSourceWriter(_fixture.Connection, _writerConfiguration);
            var root = Root.Factory();
            root.Behavior(token);
            await writer.WriteStreamAsync(stream, root, Guid.NewGuid(), Guid.NewGuid());

            var sut = CreateSut();

            var result = await sut.TryLoadAsync(stream);

            Assert.Equal(token, result.Token);            
        }


        [Fact]
        public async Task SaveAsyncHasExpectedResultWhenNoStream()
        {
            var stream = new StreamName(Guid.NewGuid().ToString());
            var token = Guid.NewGuid().ToString();
            
            var root = Root.Factory();
            root.Behavior(token);

            var sut = CreateSut();

            await sut.SaveAsync(stream, root, Guid.NewGuid(), Guid.NewGuid());

            var reader = new EventSourceReader(Root.Factory, _fixture.Connection, _readerConfiguration);
            var result = await reader.ReadStreamAsync(stream);
            Assert.Equal(ReadResultState.Found, result.State);
            Assert.Equal(token, ((Root)result.Value).Token);
        }

        [Fact]
        public async Task SaveAsyncHasExpectedResultWhenStreamPresent()
        {
            var stream = new StreamName(Guid.NewGuid().ToString());
            var token1 = Guid.NewGuid().ToString();
            var writer = new EventSourceWriter(_fixture.Connection, _writerConfiguration);
            var root = Root.Factory();
            root.Behavior(token1);
            await writer.WriteStreamAsync(stream, root, Guid.NewGuid(), Guid.NewGuid());

            var sut = CreateSut();
            var token2 = Guid.NewGuid().ToString();
            root.Behavior(token2);

            await sut.SaveAsync(stream, root, Guid.NewGuid(), Guid.NewGuid());

            var reader = new EventSourceReader(Root.Factory, _fixture.Connection, _readerConfiguration);
            var result = await reader.ReadStreamAsync(stream);
            Assert.Equal(ReadResultState.Found, result.State);
            Assert.Equal(token2, ((Root)result.Value).Token);
        }
    }
}