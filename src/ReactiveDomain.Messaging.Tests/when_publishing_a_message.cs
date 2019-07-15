using System;
using System.Threading;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests {
    // ReSharper disable once InconsistentNaming
    public sealed class when_publishing_a_message : TestWrapper,
        IHandle<TestEvent>,
        IHandle<ParentTestEvent>,
        IHandle<ChildTestEvent>,
        IHandle<GrandChildTestEvent> {
        private int _testEventCount;
        private int _parentTestEventCount;
        private int _childTestEventCount;
        private int _grandChildTestEventCount;
        private IBus _bus;

        // Called before every test (see TestWrapper)
        protected override void Reset() {
            _bus = new InMemoryBus("testBus");
            _testEventCount = 0;
            _parentTestEventCount = 0;
            _childTestEventCount = 0;
            _grandChildTestEventCount = 0;
        }

        [Fact]
        public void TestPublishSimpleMessage() {
            _bus.Subscribe<TestEvent>(this);
            _bus.Publish(new TestEvent());
            AssertEx.IsOrBecomesTrue(() => _testEventCount == 1, msg: $"Expected 1 got {_testEventCount}");
            AssertEx.IsOrBecomesTrue(() => _parentTestEventCount == 0, msg: $"Expected 0 got {_parentTestEventCount}");
            AssertEx.IsOrBecomesTrue(() => _childTestEventCount == 0, msg: $"Expected 0 got {_childTestEventCount}");
            AssertEx.IsOrBecomesTrue(() => _grandChildTestEventCount == 0, msg: $"Expected 0 got {_grandChildTestEventCount}");
        }

        [Fact]
        public void TestParentTestMessage() {
            // When subscribing to ParentTest Event the appropriate handler will be invoked when
            //  child (descendant) message types are published.
            _bus.Subscribe<ParentTestEvent>(this);
            var parentTestEvent = new ParentTestEvent();
            _bus.Publish(parentTestEvent);
            AssertEx.IsOrBecomesTrue(() => _testEventCount == 0, msg: $"Expected 0 got {_testEventCount}");
            AssertEx.IsOrBecomesTrue(() => _parentTestEventCount == 1, msg: $"Expected 1 got {_parentTestEventCount}");
            AssertEx.IsOrBecomesTrue(() => _childTestEventCount == 0, msg: $"Expected 0 got {_childTestEventCount}");
            AssertEx.IsOrBecomesTrue(() => _grandChildTestEventCount == 0, msg: $"Expected 0 got {_grandChildTestEventCount}");

            _bus.Subscribe<ChildTestEvent>(this);
            var childTestEvent = new ChildTestEvent();
            _bus.Publish(childTestEvent);
            AssertEx.IsOrBecomesTrue(() => _testEventCount == 0, msg: $"Expected 0 got {_testEventCount}");
            AssertEx.IsOrBecomesTrue(() => _parentTestEventCount == 2, msg: $"Expected 2 got {_parentTestEventCount}");
            AssertEx.IsOrBecomesTrue(() => _childTestEventCount == 1, msg: $"Expected 1 got {_childTestEventCount}");
            AssertEx.IsOrBecomesTrue(() => _grandChildTestEventCount == 0, msg: $"Expected 0 got {_grandChildTestEventCount}");

            _bus.Subscribe<GrandChildTestEvent>(this);
            var grandChildTestEvent = new GrandChildTestEvent();
            _bus.Publish(grandChildTestEvent);
            AssertEx.IsOrBecomesTrue(() => _testEventCount == 0, msg: $"Expected 0 got {_testEventCount}");
            AssertEx.IsOrBecomesTrue(() => _parentTestEventCount == 3, msg: $"Expected 3 got {_parentTestEventCount}");
            AssertEx.IsOrBecomesTrue(() => _childTestEventCount == 2, msg: $"Expected 2 got {_childTestEventCount}");
            AssertEx.IsOrBecomesTrue(() => _grandChildTestEventCount == 1, msg: $"Expected 1 got {_grandChildTestEventCount}");
        }
        [Fact]
        public void TestPublishSimpleMessageWithoutDerived() {
            _bus.Subscribe<ChildTestEvent>(this, false);
            _bus.Publish(new ParentTestEvent());
            _bus.Publish(new ChildTestEvent());
            _bus.Publish(new GrandChildTestEvent()); ;
           
            AssertEx.IsOrBecomesTrue(() => _testEventCount == 0, msg: $"Expected 1 got {_testEventCount}");
            AssertEx.IsOrBecomesTrue(() => _parentTestEventCount == 0, msg: $"Expected 0 got {_parentTestEventCount}");
            AssertEx.IsOrBecomesTrue(() => _childTestEventCount == 1, msg: $"Expected 0 got {_childTestEventCount}");
            AssertEx.IsOrBecomesTrue(() => _grandChildTestEventCount == 0, msg: $"Expected 0 got {_grandChildTestEventCount}");
        }
        [Fact]
        public void TestUnsubscribeTestMessage() {
            _bus.Subscribe<ParentTestEvent>(this);
            _bus.Publish(new ParentTestEvent());
            AssertEx.IsOrBecomesTrue(() => _testEventCount == 0, msg: $"Expected 0 got {_testEventCount}");
            AssertEx.IsOrBecomesTrue(() => _parentTestEventCount == 1, msg: $"Expected 1 got {_parentTestEventCount}");
            AssertEx.IsOrBecomesTrue(() => _childTestEventCount == 0, msg: $"Expected 0 got {_childTestEventCount}");
            AssertEx.IsOrBecomesTrue(() => _grandChildTestEventCount == 0, msg: $"Expected 0 got {_grandChildTestEventCount}");
            _bus.Publish(new GrandChildTestEvent());
            AssertEx.IsOrBecomesTrue(() => _testEventCount == 0, msg: $"Expected 0 got {_testEventCount}");
            AssertEx.IsOrBecomesTrue(() => _parentTestEventCount == 2, msg: $"Expected 1 got {_parentTestEventCount}");
            AssertEx.IsOrBecomesTrue(() => _childTestEventCount == 0, msg: $"Expected 0 got {_childTestEventCount}");
            AssertEx.IsOrBecomesTrue(() => _grandChildTestEventCount == 0, msg: $"Expected 0 got {_grandChildTestEventCount}");
            // Now unsubscribe (the point of this test)
            _bus.Unsubscribe<ParentTestEvent>(this);


            _bus.Subscribe<ChildTestEvent>(this);
            _bus.Publish(new ChildTestEvent());
            AssertEx.IsOrBecomesTrue(() => _testEventCount == 0, msg: $"Expected 0 got {_testEventCount}");
            // Parent count should not have increased
            AssertEx.IsOrBecomesTrue(() => _parentTestEventCount == 2, msg: $"Expected 1 got {_parentTestEventCount}");
            AssertEx.IsOrBecomesTrue(() => _childTestEventCount == 1, msg: $"Expected 1 got {_childTestEventCount}");
            AssertEx.IsOrBecomesTrue(() => _grandChildTestEventCount == 0, msg: $"Expected 0 got {_grandChildTestEventCount}");
            _bus.Publish(new GrandChildTestEvent());
            AssertEx.IsOrBecomesTrue(() => _testEventCount == 0, msg: $"Expected 0 got {_testEventCount}");
            // Parent count should not have increased
            AssertEx.IsOrBecomesTrue(() => _parentTestEventCount == 2, msg: $"Expected 1 got {_parentTestEventCount}");
            AssertEx.IsOrBecomesTrue(() => _childTestEventCount == 2, msg: $"Expected 1 got {_childTestEventCount}");
            AssertEx.IsOrBecomesTrue(() => _grandChildTestEventCount == 0, msg: $"Expected 0 got {_grandChildTestEventCount}");
            _bus.Unsubscribe<ChildTestEvent>(this);
            _bus.Publish(new ParentTestEvent());
            _bus.Publish(new ChildTestEvent());
            _bus.Publish(new GrandChildTestEvent());
            AssertEx.IsOrBecomesTrue(() => _testEventCount == 0, msg: $"Expected 0 got {_testEventCount}");
            // Parent count should not have increased
            AssertEx.IsOrBecomesTrue(() => _parentTestEventCount == 2, msg: $"Expected 1 got {_parentTestEventCount}");
            AssertEx.IsOrBecomesTrue(() => _childTestEventCount == 2, msg: $"Expected 1 got {_childTestEventCount}");
            AssertEx.IsOrBecomesTrue(() => _grandChildTestEventCount == 0, msg: $"Expected 0 got {_grandChildTestEventCount}");

        }

        [Fact]
        public void TestHasSubscriber() {
            var sub = _bus.Subscribe<ParentTestEvent>(this);
            Assert.True(_bus.HasSubscriberFor<ParentTestEvent>());
            sub.Dispose();
            Assert.False(_bus.HasSubscriberFor<ParentTestEvent>());

            sub = _bus.Subscribe<TestEvent>(this);
            Assert.True(_bus.HasSubscriberFor<TestEvent>());
            _bus.Unsubscribe<TestEvent>(this);
            Assert.False(_bus.HasSubscriberFor<TestEvent>());

            long gotAdHoc = 0;
            var sub2 = _bus.Subscribe(new AdHocHandler<TestEvent>(_ => Interlocked.Increment(ref gotAdHoc)));
            _bus.Publish(new TestEvent());
            Assert.True(_bus.HasSubscriberFor<TestEvent>());
            Assert.False(_bus.HasSubscriberFor<Message>()); //is anyone subscibed to message
            Assert.True(_bus.HasSubscriberFor<Message>(true)); //is anyone subscried to a derived class of mesage
            AssertEx.IsOrBecomesTrue(() => gotAdHoc == 1);

            sub = _bus.Subscribe<TestEvent>(this);
            _bus.Publish(new TestEvent());
            Assert.True(_bus.HasSubscriberFor<TestEvent>());
            Assert.False(_bus.HasSubscriberFor<Message>());
            Assert.True(_bus.HasSubscriberFor<Message>(true));
            AssertEx.IsOrBecomesTrue(() => gotAdHoc == 2);
            AssertEx.IsOrBecomesTrue(() => _testEventCount == 1);

            sub.Dispose();
            _bus.Publish(new TestEvent());
            Assert.True(_bus.HasSubscriberFor<TestEvent>());
            Assert.False(_bus.HasSubscriberFor<Message>());
            Assert.True(_bus.HasSubscriberFor<Message>(true));
            AssertEx.IsOrBecomesTrue(() => gotAdHoc == 3);
            AssertEx.IsOrBecomesTrue(() => _testEventCount == 1);

            sub2.Dispose();
            _bus.Publish(new TestEvent());
            Assert.False(_bus.HasSubscriberFor<Message>());
            Assert.False(_bus.HasSubscriberFor<Message>(true));
            Assert.False(_bus.HasSubscriberFor<TestEvent>());
            AssertEx.IsOrBecomesTrue(() => gotAdHoc == 3);
            AssertEx.IsOrBecomesTrue(() => _testEventCount == 1);
        }

        [Fact]
        public void TestNoSubscriber() {
            var testEvent = new TestEvent();
            _bus.Publish(testEvent);
            AssertEx.IsOrBecomesTrue(() => _testEventCount == 0, msg: $"Expected 0 got {_testEventCount}");
        }

        void IHandle<TestEvent>.Handle(TestEvent message) => Interlocked.Increment(ref _testEventCount);
        void IHandle<ParentTestEvent>.Handle(ParentTestEvent message) => Interlocked.Increment(ref _parentTestEventCount);
        void IHandle<ChildTestEvent>.Handle(ChildTestEvent message) => Interlocked.Increment(ref _childTestEventCount);
        void IHandle<GrandChildTestEvent>.Handle(GrandChildTestEvent message) => Interlocked.Increment(ref _grandChildTestEventCount);
    }

    public abstract class TestWrapper : IDisposable {
        // Simple wrapper to assure call to Reset before every test.
        protected TestWrapper() {
            this.Reset();
        }

        protected abstract void Reset();

        void IDisposable.Dispose() {
            // No-op
        }
    }
}