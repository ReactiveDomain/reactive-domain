using System.Threading;
using ReactiveDomain.Bus;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Tests.Helpers
{
    //No cancellation support
    public class TestCommandHandler :
        IHandleCommand<TestCommands.TestCommand3>
    {
      
        public void RequestCancel(CancelCommand cancelRequest)
        {
           //ignore cancel
        }

        public CommandResponse Handle(TestCommands.TestCommand3 command)
        {
            for (int i = 0; i < 5; i++)
            {
                Thread.Sleep(50);
            }
            return command.Succeed();
        }

 
    }
}
