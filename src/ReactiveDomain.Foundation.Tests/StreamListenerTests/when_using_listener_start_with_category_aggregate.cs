using ReactiveDomain.Foundation.Tests.StreamListenerTests.Common;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests.StreamListenerTests;

// ReSharper disable once InconsistentNaming
public sealed class when_using_listener_start_with_category_aggregate : IClassFixture<StreamStoreConnectionFixture> {
	private readonly IEventSerializer _eventSerializer = new JsonMessageSerializer();

	public when_using_listener_start_with_category_aggregate(StreamStoreConnectionFixture fixture) {
		var streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder();
		var conn = fixture.Connection;
		conn.Connect();

		var aggStream = streamNameBuilder.GenerateForAggregate(typeof(AggregateCategoryTestAggregate), Guid.NewGuid());
		var categoryStream = streamNameBuilder.GenerateForCategory(typeof(AggregateCategoryTestAggregate));

		// Drop an event into the stream testAggregate-guid
		var result = conn.AppendToStream(
			aggStream,
			ExpectedVersion.NoStream,
			null,
			_eventSerializer.Serialize(new TestEvent()));
		Assert.Equal(0, result.NextExpectedVersion);

		//wait for the projection to be written.
		CommonHelpers.WaitForStream(conn, categoryStream);

		// Now set up the projection listener, and start it. 
		var listener = new QueuedStreamListener(
			"category listener",
			conn,
			streamNameBuilder,
			new JsonMessageSerializer());
		listener.EventStream.Subscribe(new AdHocHandler<TestEvent>(Handle));
		listener.Start<AggregateCategoryTestAggregate>();
	}

	private long _testEventCount;
	[Fact]
	public void can_get_events_from_category_projection() {
		AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _testEventCount) == 1, TestTimeouts.ThrottleWaitFor);
	}

	private void Handle(IMessage message) {
		dynamic evt = message;
		if (evt is TestEvent) {
			Interlocked.Increment(ref _testEventCount);
		}

	}
	public class AggregateCategoryTestAggregate : EventDrivenStateMachine;
}
