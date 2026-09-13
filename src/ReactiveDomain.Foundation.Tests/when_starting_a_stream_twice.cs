using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

/// <summary>
/// One live listener per stream: a second would hand the model's single set of handlers every event
/// on that stream twice, and put the stream in <see cref="ReadModelBase.GetCheckpoint"/> twice.
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class when_starting_a_stream_twice : IClassFixture<StreamStoreConnectionFixture>, IDisposable {
	private readonly IStreamStoreConnection _conn;
	private readonly IConfiguredConnection _configured;
	private readonly IEventSerializer _serializer = new JsonMessageSerializer();
	private readonly IStreamNameBuilder _namer =
		new PrefixedCamelCaseStreamNameBuilder(nameof(when_starting_a_stream_twice));
	private readonly List<IDisposable> _disposables = [];

	public when_starting_a_stream_twice(StreamStoreConnectionFixture fixture) {
		_conn = fixture.Connection;
		_conn.Connect();
		_configured = new ConfiguredConnection(_conn, _namer, _serializer);
	}

	private string NewStream() => _namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());

	private void AppendEvents(string streamName, int count, int value) {
		for (var i = 0; i < count; i++) {
			_conn.AppendToStream(streamName, ExpectedVersion.Any, null,
				_serializer.Serialize(new CountedEvent(value)));
		}
	}

	private T Track<T>(T disposable) where T : IDisposable {
		_disposables.Add(disposable);
		return disposable;
	}

	[Fact]
	public async Task the_second_start_is_refused() {
		var stream = NewStream();
		AppendEvents(stream, 5, 2);
		var rm = Track(new CountingReadModel(_configured));
		rm.StartAsync(stream);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		var thrown = Assert.Throws<InvalidOperationException>(() => rm.StartAsync(stream));

		Assert.Contains(stream, thrown.Message);
		Assert.Contains("already listening", thrown.Message);
	}

	/// <summary>The category and named overloads refuse each other, since they resolve to one name.</summary>
	[Fact]
	public async Task the_overloads_refuse_each_other() {
		var rm = Track(new CountingReadModel(_configured));
		rm.StartAsync<TestAggregate>();
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		Assert.Throws<InvalidOperationException>(
			() => rm.StartAsync(_namer.GenerateForCategory(typeof(TestAggregate))));
	}

	[Fact]
	public async Task the_events_are_folded_once() {
		var stream = NewStream();
		AppendEvents(stream, 5, 2);
		var rm = Track(new CountingReadModel(_configured));

		rm.StartAsync(stream);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);
		Assert.Throws<InvalidOperationException>(() => rm.StartAsync(stream));

		Assert.Equal(10, rm.Sum);
		Assert.Single(rm.GetCheckpoint());
	}

	public void Dispose() {
		_disposables.ForEach(d => d.Dispose());
	}

	private sealed class CountingReadModel : ReadModelBase, IHandle<CountedEvent> {
		public CountingReadModel(IConfiguredConnection connection) : base(nameof(CountingReadModel), connection) {
			// ReSharper disable once RedundantTypeArgumentsOfMethod
			EventStream.Subscribe<CountedEvent>(this);
		}

		public long Sum { get; private set; }

		public void Handle(CountedEvent @event) => Sum += @event.Value;
	}

	public record CountedEvent(int Value) : Event;
}
