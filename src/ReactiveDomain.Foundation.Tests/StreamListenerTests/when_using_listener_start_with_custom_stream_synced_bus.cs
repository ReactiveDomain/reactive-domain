using System.Reactive;
using ReactiveDomain.Foundation.Tests.StreamListenerTests.Common;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests.StreamListenerTests;

// ReSharper disable once InconsistentNaming
public sealed class when_using_listener_start_with_custom_stream_synced_bus : IClassFixture<StreamStoreConnectionFixture>, IDisposable {
	private readonly JsonMessageSerializer _eventSerializer = new();
	private readonly IStreamStoreConnection _conn;
	private readonly StreamListener _listener;
	private readonly IDisposable _subscriptionDisposer;

	public when_using_listener_start_with_custom_stream_synced_bus(StreamStoreConnectionFixture fixture) {
		_conn = fixture.Connection;
		_conn.Connect();

		// Build an origin stream from strings to which the events are appended
		var originStreamName = $"testStream-{Guid.NewGuid():N}";

		var result = fixture.Connection.AppendToStream(
			originStreamName,
			ExpectedVersion.NoStream,
			null,
			_eventSerializer.Serialize(new TestEvent()));
		Assert.Equal(0, result.NextExpectedVersion);

		// Wait for the stream to be written
		CommonHelpers.WaitForStream(_conn, originStreamName);

		_listener = new QueuedStreamListener(
			originStreamName,
			fixture.Connection,
			new PrefixedCamelCaseStreamNameBuilder(),
			_eventSerializer,
			"BUS_NAME",
			LiveProcessingStarted);
		_subscriptionDisposer = _listener.EventStream.Subscribe(new AdHocHandler<Event>(Handle));
		_listener.Start(originStreamName);
	}

	private long _testEventCount;
	private long _gotLiveStarted;

	private void LiveProcessingStarted(Unit _) {
		Interlocked.Increment(ref _gotLiveStarted);
	}
	[Fact]
	public void can_get_events_from_custom_stream() {
		AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _testEventCount) == 1, TestTimeouts.ThrottleWaitFor);
		AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _gotLiveStarted) == 1, TestTimeouts.ThrottleWaitFor);
	}

	private void Handle(IMessage message) {
		dynamic evt = message;
		if (evt is TestEvent) {
			Interlocked.Increment(ref _testEventCount);
		}
	}

	public void Dispose() {
		_conn.Dispose();
		_listener.Dispose();
		_subscriptionDisposer.Dispose();
	}
}
