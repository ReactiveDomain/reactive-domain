using System;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber
{
    // ReSharper disable once InconsistentNaming
    public sealed class can_handle_inherited_messages:
                            IDisposable 
    {
        private readonly QueuedSubscriberFixture _fixture;
        
        public can_handle_inherited_messages()
        {
            _fixture = new QueuedSubscriberFixture();
        }
        [Fact]
        public void queued_subscriber_honors_subscription_inheritance()
        {
            Assert.IsOrBecomesTrue(()=> _fixture.Idle,2000,"Fixture not ready");
            _fixture.Clear();

            var testEvent = new TestEvent(CorrelatedMessage.NewRoot());
            _fixture.Publish(testEvent);
            Assert.IsOrBecomesTrue(() => _fixture.TestEventCount == 1,2000,
                msg:$"Expected {nameof(TestEvent)} 1 got {_fixture.TestEventCount}");

            var parentTestEvent = new ParentTestEvent(testEvent);
            _fixture.Publish(parentTestEvent);
            Assert.IsOrBecomesTrue(() => _fixture.TestEventCount == 1,
            msg:$"Expected {nameof(TestEvent)} 1 got {_fixture.TestEventCount}");
            Assert.IsOrBecomesTrue(() => _fixture.ParentEventCount == 1,
            msg:$"Expected {nameof(ParentTestEvent)} 1 got {_fixture.ParentEventCount}");

            var childTestEvent = new ChildTestEvent(parentTestEvent);
            _fixture.Publish(childTestEvent);
            Assert.IsOrBecomesTrue(() => _fixture.TestEventCount == 1,
                msg:$"Expected {nameof(TestEvent)} 1 got {_fixture.TestEventCount}");
            Assert.IsOrBecomesTrue(() => _fixture.ParentEventCount == 2,
                msg:$"Expected {nameof(ParentTestEvent)} 2 got {_fixture.ChildEventCount}");
            Assert.IsOrBecomesTrue(() => _fixture.ChildEventCount == 1,
                msg:$"Expected {nameof(ChildTestEvent)} 1 got {_fixture.TestEventCount}");

            _fixture.Publish(new GrandChildTestEvent(childTestEvent));
            Assert.IsOrBecomesTrue(() => _fixture.TestEventCount == 1,
                msg:$"Expected {nameof(TestEvent)} 1 got {_fixture.TestEventCount}");
            Assert.IsOrBecomesTrue(() => _fixture.ParentEventCount == 3,
                msg:$"Expected {nameof(ParentTestEvent)} 3 got {_fixture.ParentEventCount}");
            Assert.IsOrBecomesTrue(() => _fixture.ChildEventCount == 2,
                msg:$"Expected {nameof(ChildTestEvent)} 2 got {_fixture.ChildEventCount}");
            Assert.IsOrBecomesTrue(() => _fixture.GrandChildEventCount == 1,
                msg:$"Expected {nameof(GrandChildTestEvent)} 1 got {_fixture.TestEventCount}");
            Assert.IsOrBecomesTrue(() => _fixture.Idle);
            _fixture.Clear();
        }

        public void Dispose() {
            _fixture?.Dispose();
        }
    }
}
