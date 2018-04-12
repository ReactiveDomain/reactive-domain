using System.Threading;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Testing
{
    //No cancellation support
    public class TestCommandHandler :
        IHandleCommand<TestCommands.Command3>
    {
   
        public CommandResponse Handle(TestCommands.Command3 command)
        {
            for (int i = 0; i < 5; i++)
            {
                Thread.Sleep(10);
            }
            return command.Succeed();
        }

 
    }
}
