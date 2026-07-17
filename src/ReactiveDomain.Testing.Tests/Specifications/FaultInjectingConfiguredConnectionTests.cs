using ReactiveDomain.Foundation;
using ReactiveDomain.Testing.EventStore;
using Xunit;

namespace ReactiveDomain.Testing.Tests.Specifications;

public sealed class FaultInjectingConfiguredConnectionTests : IDisposable {
	private readonly MockStreamStoreConnection _conn = new(nameof(FaultInjectingConfiguredConnectionTests));
	private readonly IConfiguredConnection _inner;
	private readonly Guid _poisonId = Guid.NewGuid();
	private readonly FaultInjectingConfiguredConnection _faulting;

	public FaultInjectingConfiguredConnectionTests() {
		_conn.Connect();
		_inner = new ConfiguredConnection(
			_conn,
			new PrefixedCamelCaseStreamNameBuilder(nameof(FaultInjectingConfiguredConnectionTests)),
			new JsonMessageSerializer());
		_faulting = new FaultInjectingConfiguredConnection(_inner, es => es.Id == _poisonId);
	}

	[Fact]
	public void save_throws_exactly_when_the_predicate_matches() {
		var repo = _faulting.GetRepository();

		var goodId = Guid.NewGuid();
		repo.Save(new TestAggregate(goodId));
		Assert.True(repo.TryGetById<TestAggregate>(goodId, out _));

		var poison = new TestAggregate(_poisonId);
		var ex = Assert.Throws<InjectedSaveException>(() => repo.Save(poison));
		Assert.Same(poison, ex.Aggregate);
		Assert.False(repo.TryGetById<TestAggregate>(_poisonId, out _)); // nothing was persisted
	}

	[Fact]
	public void correlated_repository_saves_fault_too() {
		var correlated = _faulting.GetCorrelatedRepository();
		Assert.Throws<InjectedSaveException>(() => correlated.Save(new TestAggregate(_poisonId)));
	}

	[Fact]
	public void reads_listeners_and_readers_pass_through() {
		//seed through the unwrapped connection
		_inner.GetRepository().Save(new TestAggregate(_poisonId));

		//reads are untouched: the poison aggregate loads fine
		Assert.True(_faulting.GetRepository().TryGetById<TestAggregate>(_poisonId, out var loaded));
		Assert.Equal(_poisonId, loaded!.Id);

		//listeners and readers come straight from the inner connection
		using var listener = _faulting.GetListener("passThrough") as StreamListener;
		Assert.NotNull(listener);
		var reader = _faulting.GetReader("passThrough", _ => { });
		Assert.NotNull(reader);
	}

	public void Dispose() {
		_conn.Dispose();
	}
}
