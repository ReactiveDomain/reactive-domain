using System;
using System.Threading;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests
{
    public class when_sending_concurrent_commands : CommandQueueSpecification
    {
        public when_sending_concurrent_commands() : base(3)
        {

        }
        private long _cmd1Count = 0;
        long _cmd2Count = 0;
        long _cmd3Count = 0;

        protected override void Given()
        {
             
             
            Bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand>(
                                    cmd => { Interlocked.Increment(ref _cmd1Count); return true; }));
            Bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand2>(
                                    cmd => { Interlocked.Increment(ref _cmd2Count); return true; }));
            Bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand3>(
                                    cmd => { Interlocked.Increment(ref _cmd3Count); return true; }));

            Assert.True(_cmd1Count == 0, $"gotSuccess should be 0, found { _cmd1Count}");

        }

        protected override void When()
        {
            for (int i = 0; i < 50; i++)
            {
                Queue.Handle(new TestCommands.TestCommand(Guid.NewGuid(), null));
                Queue.Handle(new TestCommands.TestCommand2(Guid.NewGuid(), null));
                Queue.Handle(new TestCommands.TestCommand3(Guid.NewGuid(), null));
            }
        }
        [Fact]
        public void concurrent_commands_should_pass()
        {
            Assert.IsOrBecomesTrue(() => _cmd3Count == 50, 50 * 55 + 200, $"gotTimeout should be 50, found { _cmd3Count}");
            Assert.IsOrBecomesTrue(() => _cmd1Count == 50, 2000, $"gotSuccess should be 50, found { _cmd1Count}");
            Assert.IsOrBecomesTrue(() => _cmd2Count == 50, 2000, $"gotFail should be 50, found { _cmd2Count}");
         }
    }
}
