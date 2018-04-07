using System;
using ReactiveDomain.Messaging.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber {
    // ReSharper disable once InconsistentNaming
    public sealed class can_handle_inherited_messages : IClassFixture<QueuedSubscriberFixture> {
        private readonly QueuedSubscriberFixture _fixture;

       
        public can_handle_inherited_messages(QueuedSubscriberFixture fixture) {
            _fixture = fixture;
        }
        [Fact]
        public void queued_subscriber_honors_subscription_inheritance() {

            var testCorrelationId = Guid.NewGuid();

            _fixture.Publish(new TestEvent(testCorrelationId, Guid.Empty));
            Assert.IsOrBecomesTrue(() => _fixture.TestEventCount == 1,msg:$"Expected 1 got {_fixture.TestEventCount}");

            _fixture.Publish(new ParentTestEvent(testCorrelationId, Guid.NewGuid()));
            Assert.IsOrBecomesTrue(() => _fixture.TestEventCount == 1);
            Assert.IsOrBecomesTrue(() => _fixture.ParentEventCount == 1);
            
            _fixture.Publish(new ChildTestEvent(testCorrelationId, Guid.NewGuid()));
            Assert.IsOrBecomesTrue(() => _fixture.TestEventCount == 1);
            Assert.IsOrBecomesTrue(() => _fixture.ParentEventCount == 2);
            Assert.IsOrBecomesTrue(() => _fixture.ChildEventCount == 1);

            _fixture.Publish(new GrandChildTestEvent(testCorrelationId, Guid.NewGuid()));
            Assert.IsOrBecomesTrue(() => _fixture.TestEventCount == 1);
            Assert.IsOrBecomesTrue(() => _fixture.ParentEventCount == 3);
            Assert.IsOrBecomesTrue(() => _fixture.ChildEventCount == 2);
            Assert.IsOrBecomesTrue(() => _fixture.GrandChildEventCount == 1);
            Assert.IsOrBecomesTrue(() => _fixture.Idle);
            _fixture.Clear();
        }
    }
}
