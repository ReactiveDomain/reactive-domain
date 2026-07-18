using ReactiveDomain.Foundation.Tests.StreamListenerTests.Common;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests.StreamListenerTests;

// ReSharper disable once InconsistentNaming
public sealed class when_using_listener_start_with_event_type : IClassFixture<StreamStoreConnectionFixture>, IDisposable {
	private readonly JsonMessageSerializer _eventSerializer = new();
	private readonly IStreamStoreConnection _conn;
	private readonly QueuedStreamListener _listener;
	private readonly IDisposable _subscriptionDisposer;

	public when_using_listener_start_with_event_type(StreamStoreConnectionFixture fixture) {
		var streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder();
		_conn = fixture.Connection;
		_conn.Connect();

		var originalAggregateStream =
			streamNameBuilder.GenerateForAggregate(
				typeof(TestAggregate),
				Guid.NewGuid());
		var evt = new EventProjectionTestEvent();

		//drop the event into the stream
		var result = _conn.AppendToStream(
			originalAggregateStream,
			ExpectedVersion.NoStream,
			null,
			_eventSerializer.Serialize(evt));
		Assert.Equal(0, result.NextExpectedVersion);

		//wait for the projection to be written
		CommonHelpers.WaitForStream(_conn, streamNameBuilder.GenerateForEventType(nameof(EventProjectionTestEvent)));

		//build the listener
		_listener = new QueuedStreamListener(
			"event listener",
			_conn,
			streamNameBuilder,
			_eventSerializer);

		_subscriptionDisposer = _listener.EventStream.Subscribe(new AdHocHandler<EventProjectionTestEvent>(Handle));
		_listener.Start(typeof(EventProjectionTestEvent));
	}

	private long _testEventCount;
	[Fact]
	public void can_get_events_from_event_type_stream() {
		AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _testEventCount) == 1, TestTimeouts.ThrottleWaitFor, "Event Not Received");
	}

	private void Handle(IMessage message) {
		dynamic evt = message;
		if (evt is EventProjectionTestEvent) {
			Interlocked.Increment(ref _testEventCount);
		}

	}
	public record EventProjectionTestEvent : Event;

	public void Dispose() {
		_conn.Dispose();
		_listener.Dispose();
		_subscriptionDisposer.Dispose();
	}
}
