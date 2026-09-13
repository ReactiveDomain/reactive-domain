using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

/// <summary>
/// The handshake end to end: a handler reports its <c>Save</c> checkpoints on <see cref="Success"/>,
/// and the sender uses them against <see cref="ReadModelBase.GetCheckpoint"/> as a read-your-writes
/// target instead of polling model state.
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class when_responding_with_write_positions : IClassFixture<StreamStoreConnectionFixture>, IDisposable {
	private readonly IConfiguredConnection _configured;
	private readonly IStreamNameBuilder _namer =
		new PrefixedCamelCaseStreamNameBuilder(nameof(when_responding_with_write_positions));
	private readonly Dispatcher _bus = new(nameof(when_responding_with_write_positions));
	private readonly List<IDisposable> _disposables = [];

	public when_responding_with_write_positions(StreamStoreConnectionFixture fixture) {
		fixture.Connection.Connect();
		_configured = new ConfiguredConnection(fixture.Connection, _namer, new JsonMessageSerializer());
		_disposables.Add(_bus);
	}

	[Fact]
	public void the_response_carries_the_handlers_write_and_the_sender_waits_on_it() {
		var handler = new ProduceHandler(_configured.GetRepository());
		_bus.Subscribe(handler);
		var id = Guid.NewGuid();

		var rm = Track(new CountingReadModel(_configured));
		rm.StartAsync<TestWoftamAggregate>(id);

		Assert.True(_bus.TrySend(new Produce(id, 5), out var response));
		var success = Assert.IsType<Success>(response);
		var write = Assert.Single(success.WritePositions);
		Assert.Equal(_namer.GenerateForAggregate(typeof(TestWoftamAggregate), id), write.StreamName);
		Assert.Equal(5, write.Version); // created + 5 produced, zero-based

		// The sender's wait: no polling of model state, no extra read of the store.
		AssertEx.IsOrBecomesTrue(
			() => StreamCheckpoint.Compare(rm.GetCheckpoint(), success.WritePositions) is CheckpointOrder.Equal or CheckpointOrder.After,
			TestTimeouts.ThrottleWaitFor);
		AssertEx.IsOrBecomesTrue(() => rm.Count == 5, TestTimeouts.ThrottleWaitFor);
	}

	[Fact]
	public void a_multi_write_handler_reports_one_entry_per_stream() {
		var handler = new ProduceTwiceHandler(_configured.GetRepository());
		_bus.Subscribe(handler);
		var first = Guid.NewGuid();
		var second = Guid.NewGuid();

		Assert.True(_bus.TrySend(new ProduceTwice(first, second), out var response));
		var success = Assert.IsType<Success>(response);

		Assert.Equal(2, success.WritePositions.Count);
		Assert.Contains(success.WritePositions, w => w.StreamName == _namer.GenerateForAggregate(typeof(TestWoftamAggregate), first) && w.Version == 2);
		Assert.Contains(success.WritePositions, w => w.StreamName == _namer.GenerateForAggregate(typeof(TestWoftamAggregate), second) && w.Version == 3);
	}

	[Fact]
	public void a_handler_that_reports_nothing_succeeds_as_before() {
		_bus.Subscribe(new SilentHandler());

		Assert.True(_bus.TrySend(new Silent(), out var response));
		var success = Assert.IsType<Success>(response);

		Assert.Empty(success.WritePositions);
		Assert.Equal(CheckpointOrder.Equal, StreamCheckpoint.Compare([], success.WritePositions));
	}

	private T Track<T>(T disposable) where T : IDisposable {
		_disposables.Add(disposable);
		return disposable;
	}

	public void Dispose() {
		_disposables.ForEach(d => d.Dispose());
	}

	public record Produce(Guid AggregateId, int Count) : Command;
	public record ProduceTwice(Guid First, Guid Second) : Command;
	public record Silent : Command;

	private sealed class ProduceHandler(IRepository repo) : IHandleCommand<Produce> {
		public CommandResponse Handle(Produce command) {
			var agg = new TestWoftamAggregate(command.AggregateId);
			agg.ProduceEvents(command.Count);
			return command.Succeed(repo.Save(agg));
		}
	}

	private sealed class ProduceTwiceHandler(IRepository repo) : IHandleCommand<ProduceTwice> {
		public CommandResponse Handle(ProduceTwice command) {
			var first = new TestWoftamAggregate(command.First);
			first.ProduceEvents(2);
			var second = new TestWoftamAggregate(command.Second);
			second.ProduceEvents(3);
			return command.Succeed(repo.Save(first), repo.Save(second));
		}
	}

	private sealed class SilentHandler : IHandleCommand<Silent> {
		public CommandResponse Handle(Silent command) => command.Succeed();
	}

	private sealed class CountingReadModel : ReadModelBase, IHandle<WoftamEvent> {
		public CountingReadModel(IConfiguredConnection connection) : base(nameof(CountingReadModel), connection) {
			// ReSharper disable once RedundantTypeArgumentsOfMethod
			EventStream.Subscribe<WoftamEvent>(this);
		}

		public int Count { get; private set; }

		public void Handle(WoftamEvent @event) => Count++;
	}
}
