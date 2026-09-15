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
		listener.Dispose();
		sw.Stop();
		Assert.True(sw.Elapsed < QueuedHandler.DefaultStopWaitTimeout,
			$"Listener Dispose took {sw.Elapsed}; the queue thread was not released.");
		Assert.Equal(200, handled);
	}

	/// <summary>
	/// The second call reaches a listener whose queue and its wait handles are already gone, so it
	/// must not run the teardown again.
	/// </summary>
	[Fact]
	public void disposing_twice_is_a_no_op() {
		var conn = new MockStreamStoreConnection(nameof(when_disposing_a_queued_stream_listener));
		conn.Connect();
		var stream = _namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());
		conn.AppendToStream(stream, ExpectedVersion.Any, null, _serializer.Serialize(new ListenerTestEvent()));

		var listener = new QueuedStreamListener(nameof(when_disposing_a_queued_stream_listener), conn, _namer, _serializer);
		listener.Start(stream);
		listener.Dispose();

		listener.Dispose();

		Assert.True(listener.IsDisposed);
	}

	[Fact]
	public void a_started_listener_is_not_disposed() {
		var conn = new MockStreamStoreConnection(nameof(when_disposing_a_queued_stream_listener));
		conn.Connect();
		var stream = _namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());
		conn.AppendToStream(stream, ExpectedVersion.Any, null, _serializer.Serialize(new ListenerTestEvent()));

		using var listener = new QueuedStreamListener(nameof(when_disposing_a_queued_stream_listener), conn, _namer, _serializer);
		listener.Start(stream);

		Assert.False(listener.IsDisposed);
	}

	/// <summary>
	/// The two are separate states: a derived class stops taking events before it tears down its own
	/// queue, and until <see cref="IDisposable.Dispose"/> runs nothing has been released.
	/// </summary>
	[Fact]
	public void stopping_a_listener_does_not_report_it_as_disposed() {
		var conn = new MockStreamStoreConnection(nameof(when_disposing_a_queued_stream_listener));
		conn.Connect();
		var stream = _namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());
		conn.AppendToStream(stream, ExpectedVersion.Any, null, _serializer.Serialize(new ListenerTestEvent()));

		using var listener = new StoppableListener(nameof(when_disposing_a_queued_stream_listener), conn, _namer, _serializer);
		listener.Start(stream);

		listener.Stop();

		Assert.True(listener.HasStopped);
		Assert.False(listener.IsDisposed);
	}

	/// <summary>Reaches <c>StopListening</c> and <c>Stopped</c>, which only a derived listener can.</summary>
	private sealed class StoppableListener(
		string name,
		IStreamStoreConnection connection,
		IStreamNameBuilder streamNameBuilder,
		IEventSerializer serializer)
		: QueuedStreamListener(name, connection, streamNameBuilder, serializer) {
		public void Stop() => StopListening();
		public bool HasStopped => Stopped;
	}
}
