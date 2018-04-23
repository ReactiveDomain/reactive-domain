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
           SpinWait.SpinUntil(()=>false, 500);
            return command.Succeed();
        }

 
    }
}
