using System.Threading;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Messaging.Testing
{
    public class TestCommandSubscriber :
                IHandleCommand<TestCommands.Command2>,
                IHandleCommand<TestCommands.Command3>
    {
        public long TestCommand2Handled;
        public long TestCommand3Handled;

        private IDispatcher _bus;

        public TestCommandSubscriber(IDispatcher bus)
        {
            _bus = bus;
            TestCommand2Handled = 0;
            _bus.Subscribe<TestCommands.Command2> (this);
            _bus.Subscribe<TestCommands.Command3>(this);
        }
              

        public CommandResponse Handle(TestCommands.Command2 command)
        {
            Interlocked.Increment(ref TestCommand2Handled);
            return command.Succeed();
        }

        public CommandResponse Handle(TestCommands.Command3 command)
        {
            Interlocked.Increment(ref TestCommand3Handled);
            return command.Succeed();
        }
    }
}
