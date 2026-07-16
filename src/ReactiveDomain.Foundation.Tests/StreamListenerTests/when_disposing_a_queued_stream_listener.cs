using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using ReactiveDomain.Testing.EventStore;
using Xunit;

namespace ReactiveDomain.Foundation.Tests.StreamListenerTests;

// Regression test for #219: disposing a QueuedStreamListener while its SyncQueue is
// draining must release the in-flight Handle and join the queue thread promptly.
// ReSharper disable once InconsistentNaming
public sealed class when_disposing_a_queued_stream_listener {
	private static readonly IEventSerializer _serializer = new JsonMessageSerializer();
	private static readonly IStreamNameBuilder _namer =
		new PrefixedCamelCaseStreamNameBuilder(nameof(when_disposing_a_queued_stream_listener));

	public record ListenerTestEvent : Event;

	[Fact]
	public void disposing_a_draining_listener_does_not_time_out() {
		var conn = new MockStreamStoreConnection(nameof(when_disposing_a_queued_stream_listener));
		conn.Connect();
		var stream = _namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());
		for (var i = 0; i < 200; i++)
			conn.AppendToStream(stream, ExpectedVersion.Any, null, _serializer.Serialize(new ListenerTestEvent()));

		var listener = new QueuedStreamListener(nameof(when_disposing_a_queued_stream_listener), conn, _namer, _serializer);
		var handled = 0;
		listener.EventStream.Subscribe(new AdHocHandler<ListenerTestEvent>(_ => {
			Thread.Sleep(2); // hold the SyncQueue thread in-handler while Dispose runs
			Interlocked.Increment(ref handled);
		}));
		listener.Start(stream);
		AssertEx.IsOrBecomesTrue(() => handled > 5, 10_000);

		var sw = System.Diagnostics.Stopwatch.StartNew();
		listener.Dispose(); // pre-fix: parked the SyncQueue thread on the _running gate and timed out
		sw.Stop();
		Assert.True(sw.Elapsed < QueuedHandler.DefaultStopWaitTimeout,
			$"Listener Dispose took {sw.Elapsed}; the queue thread was not released.");
	}
}
