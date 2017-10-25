using System.Threading;
using ReactiveDomain.Bus;
using ReactiveDomain.Messaging;
using ReactiveDomain.Tests.Helpers;

namespace ReactiveDomain.Tests.Subscribers.QueuedSubscriber
{
    public class TestCommandSubscriber :
                IHandleCommand<InformUserCmd>,
                IHandleCommand<TestCommands.TestCommand3>
    {
        public long CommandHandled;
        private IGeneralBus _bus;

        public TestCommandSubscriber(IGeneralBus bus)
        {
            _bus = bus;
            CommandHandled = 0;
            _bus.Subscribe<InformUserCmd> (this);
            _bus.Subscribe<TestCommands.TestCommand3>(this);
        }


        public void RequestCancel(CancelCommand cancelRequest)
        {

        }

        public CommandResponse Handle(InformUserCmd command)
        {
            Interlocked.Increment(ref CommandHandled);
            return command.Succeed();
        }

        public CommandResponse Handle(TestCommands.TestCommand3 command)
        {
            return command.Succeed();
        }
    }
}
