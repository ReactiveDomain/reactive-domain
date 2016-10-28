using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveDomain.Bus;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Tests.Helpers
{
    public class TestMessageSubscriber :
        QueuedSubscriber,
        IHandle<TestMessage>,
        IHandle<TestMessage2>,
        IHandle<ChildTestMessage>,
        IHandle<ParentTestMessage>
    {
        public long TimesChildTestMessageHandled;
        public long ParentTestMessage;
        public long TimesTestMessageHandled;
        public long TimesTestMessage2Handled;
        public override void HandleDynamic(dynamic message)
        {
            Handle(message);
        }

        public TestMessageSubscriber(IGeneralBus bus) : base(bus)
        {
            Subscribe<TestMessage>(this);
            Subscribe<TestMessage2>(this);
            Subscribe<ChildTestMessage>(this);
            //Subscribe<ParentTestMessage>(this);

            TimesTestMessageHandled = 0;
            TimesTestMessage2Handled = 0;
            TimesChildTestMessageHandled = 0;
            ParentTestMessage = 0;
        }

        public void Handle(TestMessage message)
        {
            TimesTestMessageHandled++;
        }

        public void Handle(TestMessage2 message)
        {
            TimesTestMessage2Handled++;
        }

        public void Handle(ChildTestMessage message)
        {
            TimesChildTestMessageHandled++;
        }

        public void Handle(ParentTestMessage message)
        {
            ParentTestMessage++;
        }
    }

    public class TestInheritedMessageSubscriber :
        QueuedSubscriber,
        IHandle<GrandChildTestMessage>,
        IHandle<ChildTestMessage>,
        IHandle<ParentTestMessage>
    {
        public long TimesChildTestMessageHandled;
        public long TimesParentTestMessageHandled;
        public long TimesGrandChildTestMessageHandled;
        public override void HandleDynamic(dynamic message)
        {
            Handle(message);
        }

        public TestInheritedMessageSubscriber(IGeneralBus bus) : base(bus)
        {
            Subscribe<GrandChildTestMessage>(this);
            Subscribe<ChildTestMessage>(this);
            Subscribe<ParentTestMessage>(this);

            TimesGrandChildTestMessageHandled = 0;
            TimesChildTestMessageHandled = 0;
            TimesParentTestMessageHandled = 0;
        }

        public void Handle(ChildTestMessage message)
        {
            TimesChildTestMessageHandled++;
        }

        public void Handle(ParentTestMessage message)
        {
            TimesParentTestMessageHandled++;
        }

        public void Handle(GrandChildTestMessage message)
        {
            TimesGrandChildTestMessageHandled++;
        }
    }
}
