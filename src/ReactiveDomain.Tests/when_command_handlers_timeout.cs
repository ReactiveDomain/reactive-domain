
using ReactiveDomain.Messaging;
using ReactiveDomain.Tests.Helpers;
using ReactiveDomain.Tests.Specifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Messaging.Bus;
using Xunit;

namespace ReactiveDomain.Tests
{
    public class when_command_handlers_timeout : CommandBusSpecification
    {
        long gotCmd1 = 0;

        protected override void Given()
        {
            Bus.Subscribe(new AdHocCommandHandler<TestCommands.TimeoutTestCommand>(
                                    cmd =>
                                    {
                                        Interlocked.Increment(ref gotCmd1);
                                        Thread.Sleep(2000);
                                        return true;
                                    }));
        }

        protected override void When()
        {
        }

        [Fact]
        public void slow_commands_should_throw_timeout()
        {
           Assert.Throws<CommandTimedOutException>(
               () => Bus.Fire(new TestCommands.TimeoutTestCommand(Guid.NewGuid(), null))
               );

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotCmd1) == 1, msg: "Expected Cmd1 handled");

            TestQueue.WaitFor<TestCommands.TimeoutTestCommand>(TimeSpan.FromMilliseconds(2000));
            TestQueue.WaitFor<Canceled>(TimeSpan.FromMilliseconds(2000));
            TestQueue.Commands
                       .AssertNext<TestCommands.TimeoutTestCommand>(_ => true)
                       .AssertEmpty();
            TestQueue.Responses
                        .AssertNext<Canceled>(msg => 
                        msg.Exception is CommandCanceledException)
                        .AssertEmpty();

        }
    }
}
