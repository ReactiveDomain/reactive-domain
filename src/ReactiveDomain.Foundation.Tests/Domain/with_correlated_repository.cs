using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Testing;
using Xunit;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation.Tests {
    [Collection(nameof(EmbeddedStreamStoreConnectionCollection))]
    // ReSharper disable once InconsistentNaming
    public sealed class with_correlated_repository  {
        private readonly StreamStoreConnectionFixture _fixture;
        private readonly ICorrelatedRepository _repo;
        private readonly IRepository _stdRepo;
        public with_correlated_repository(StreamStoreConnectionFixture fixture) {
            _fixture = fixture;
            fixture.Connection.Connect();
            _stdRepo = new StreamStoreRepository(new PrefixedCamelCaseStreamNameBuilder(),
                fixture.Connection,
                new JsonMessageSerializer());

            _repo = new CorrelatedStreamStoreRepository(
                            _stdRepo);
        }

        [Fact]
        public void can_retrieve_correlated_aggregate() {
            var command1 = MessageBuilder.New(() => new TestCommands.Command1());
            var id = Guid.NewGuid();
            var agg = new CorrelatedAggregate(id, command1);
            agg.RaiseCorrelatedEvent();
            agg.RaiseCorrelatedEvent();
            _repo.Save(agg);

            var command2 = MessageBuilder.New(() => new TestCommands.Command2());
            var recovered = _repo.GetById<CorrelatedAggregate>(id, command2);
            Assert.NotNull(recovered);
            Assert.Equal(2, recovered.Version);//zero based, includes created

            recovered.RaiseCorrelatedEvent();
            recovered.RaiseCorrelatedEvent();

            _repo.Save(recovered);
            var command3 = MessageBuilder.New(() => new TestCommands.Command3());
            var recovered2 = _repo.GetById<CorrelatedAggregate>(id, command3);

            Assert.NotNull(recovered2);
            Assert.Equal(4, recovered2.Version);
        }

        [Fact]
        public void source_is_not_persisted() {
            var command1 = MessageBuilder.New(() => new TestCommands.Command1());
            var id = Guid.NewGuid();
            var agg = new CorrelatedAggregate(id, command1);
            agg.RaiseCorrelatedEvent();
            agg.RaiseCorrelatedEvent();
            _stdRepo.Save(agg);

            var command2 = MessageBuilder.New(() => new TestCommands.Command2());
            var recovered = _stdRepo.GetById<CorrelatedAggregate>(id);
            Assert.NotNull(recovered);
            Assert.Equal(2, recovered.Version);//zero based, includes created

            //no source has been set
            Assert.Throws<InvalidOperationException>(() => recovered.RaiseCorrelatedEvent());
            ((ICorrelatedEventSource)recovered).Source =command2;
            recovered.RaiseCorrelatedEvent();
        }
    }
}
