using ReactiveDomain.Foundation.Tests.StreamListenerTests.Common;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests.StreamListenerTests;

// ReSharper disable once InconsistentNaming
public sealed class when_using_listener_start_with_custom_stream_not_synced : IClassFixture<StreamStoreConnectionFixture> {
	private readonly JsonMessageSerializer _eventSerializer = new();

	public when_using_listener_start_with_custom_stream_not_synced(StreamStoreConnectionFixture fixture) {
		var conn = fixture.Connection;
		conn.Connect();

		// Build an origin stream from strings to which the events are appended
		var originStreamName = $"testStream-{Guid.NewGuid():N}";

		// Generate event and save it to the custom stream

		var result = fixture.Connection.AppendToStream(
			originStreamName,
			ExpectedVersion.NoStream,
			null,
			_eventSerializer.Serialize(new TestEvent()));
		Assert.Equal(0, result.NextExpectedVersion);

		// Wait for the stream to be written
		CommonHelpers.WaitForStream(conn, originStreamName);

		StreamListener listener = new QueuedStreamListener(
			originStreamName,
			fixture.Connection,
			new PrefixedCamelCaseStreamNameBuilder(),
			_eventSerializer);
		listener.EventStream.Subscribe(new AdHocHandler<Event>(Handle));
		listener.Start(originStreamName);
	}

	private long _testEventCount;

	[Fact]
	public void can_get_events_from_custom_stream() {
		AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _testEventCount) == 1, 3000);
	}

	private void Handle(IMessage message) {
		dynamic evt = message;
		if (evt is TestEvent) {
			Interlocked.Increment(ref _testEventCount);
		}
	}
}
