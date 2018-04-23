using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber
{
    // ReSharper disable once InconsistentNaming
    public sealed class can_handle_inherited_messages : IClassFixture<QueuedSubscriberFixture>
    {
        private readonly QueuedSubscriberFixture _fixture;
        
        public can_handle_inherited_messages(QueuedSubscriberFixture fixture)
        {
            _fixture = fixture;
        }
        [Fact]
        public void queued_subscriber_honors_subscription_inheritance() {
            Assert.IsOrBecomesTrue(() => _fixture.Idle,2000);
            _fixture.Clear();
            var testEvent = new TestEvent(CorrelatedMessage.NewRoot());
            _fixture.Publish(testEvent);
            Assert.IsOrBecomesTrue(() => _fixture.TestEventCount == 1,msg:$"Expected 1 got {_fixture.TestEventCount}");

            var parentTestEvent = new ParentTestEvent(testEvent);
            _fixture.Publish(parentTestEvent);
            Assert.IsOrBecomesTrue(() => _fixture.TestEventCount == 1);
            Assert.IsOrBecomesTrue(() => _fixture.ParentEventCount == 1);

            var childTestEvent = new ChildTestEvent(parentTestEvent);
            _fixture.Publish(childTestEvent);
            Assert.IsOrBecomesTrue(() => _fixture.TestEventCount == 1);
            Assert.IsOrBecomesTrue(() => _fixture.ParentEventCount == 2);
            Assert.IsOrBecomesTrue(() => _fixture.ChildEventCount == 1);

            _fixture.Publish(new GrandChildTestEvent(childTestEvent));
            Assert.IsOrBecomesTrue(() => _fixture.TestEventCount == 1);
            Assert.IsOrBecomesTrue(() => _fixture.ParentEventCount == 3);
            Assert.IsOrBecomesTrue(() => _fixture.ChildEventCount == 2);
            Assert.IsOrBecomesTrue(() => _fixture.GrandChildEventCount == 1);
            Assert.IsOrBecomesTrue(() => _fixture.Idle);
            _fixture.Clear();
        }
    }
}
