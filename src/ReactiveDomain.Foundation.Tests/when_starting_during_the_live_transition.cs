using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

/// <summary>
/// <c>CompleteLiveTransition</c> claims the armed task, runs the waiting callbacks, and only then
/// signals it. This test lands a <c>Start</c> and an <c>OnceLive</c> inside that window — held open
/// by a callback parked on a gate — and pins that the claim counts as completed: the start arms a
/// fresh task instead of being counted against one about to complete (<c>IsLive</c> returning with
/// its history still queued), and the callback is queued instead of waiting until dispose.
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class when_starting_during_the_live_transition : IClassFixture<StreamStoreConnectionFixture>, IDisposable {
	private readonly IStreamStoreConnection _conn;
	private readonly IConfiguredConnection _configured;
	private readonly IEventSerializer _serializer = new JsonMessageSerializer();
	private readonly IStreamNameBuilder _namer =
		new PrefixedCamelCaseStreamNameBuilder(nameof(when_starting_during_the_live_transition));
	private readonly List<IDisposable> _disposables = [];

	public when_starting_during_the_live_transition(StreamStoreConnectionFixture fixture) {
		_conn = fixture.Connection;
		_conn.Connect();
		_configured = new ConfiguredConnection(_conn, _namer, _serializer);
	}

	private string NewStream() => _namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());

	private void AppendEvents(string streamName, int count, int value) {
		for (var i = 0; i < count; i++) {
			_conn.AppendToStream(streamName, ExpectedVersion.Any, null,
				_serializer.Serialize(new WindowTestEvent(value)));
		}
	}

	private T Track<T>(T disposable) where T : IDisposable {
		_disposables.Add(disposable);
		return disposable;
	}

	[Fact]
	public async Task a_start_and_a_callback_landing_in_the_window_are_not_lost() {
		var first = NewStream();
		var second = NewStream();
		AppendEvents(first, 1, 1);
		AppendEvents(second, 3, 10);
		using var readGate = new ManualResetEventSlim(false);
		using var enteredWindow = new ManualResetEventSlim(false);
		using var exitWindow = new ManualResetEventSlim(false);
		// The first read waits on the gate, so the callback below registers while the stream is still
		// pending and runs at the transition — between the claim and the signal.
		var connection = new HookedConnection(_configured, beforeRead: s => {
			if (s == first) { readGate.Wait(TestTimeouts.ThrottleWaitFor); }
		});
		var rm = Track(new WindowTestReadModel(connection));

		rm.StartAsync(first);
		var gate = rm.OnceLive(() => {
			enteredWindow.Set();
			exitWindow.Wait(TestTimeouts.ThrottleWaitFor);
		});
		readGate.Set();
		Assert.True(enteredWindow.Wait(TestTimeouts.ThrottleWaitFor));

		// Inside the window: the first stream's task is claimed but not signalled.
		rm.StartAsync(second);
		var live = rm.IsLive;
		var registered = rm.OnceLive(() => { });
		exitWindow.Set();

		await live.WaitAsync(TestTimeouts.ThrottleWaitFor);
		Assert.Equal(31, rm.Sum); // 1 + 3 x 10: live covered the second stream's history
		await registered.WaitAsync(TestTimeouts.ThrottleWaitFor);
		await gate.WaitAsync(TestTimeouts.ThrottleWaitFor);
	}

	public void Dispose() {
		_disposables.ForEach(d => d.Dispose());
	}

	private sealed class WindowTestReadModel : ReadModelBase, IHandle<WindowTestEvent> {
		public WindowTestReadModel(IConfiguredConnection connection)
			: base(nameof(WindowTestReadModel), connection) {
			// ReSharper disable once RedundantTypeArgumentsOfMethod
			EventStream.Subscribe<WindowTestEvent>(this);
		}

		public long Sum { get; private set; }

		public void Handle(WindowTestEvent @event) => Sum += @event.Value;
	}

	public record WindowTestEvent(int Value) : Event;
}
