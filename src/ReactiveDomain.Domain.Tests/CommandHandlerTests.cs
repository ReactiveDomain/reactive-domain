using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ReactiveDomain
{
    public class CommandHandlerTests
    {
        [Fact]
        public void CommandCanNotBeNull()
        {
            Assert.Throws<ArgumentNullException>(() => new CommandHandler(null, (e, ct) => Task.CompletedTask));
        }

        [Fact]
        public void HandlerCanNotBeNull()
        {
            Assert.Throws<ArgumentNullException>(() => new CommandHandler(typeof(object), null));
        }

        [Fact]
        public void PropertiesReturnExpectedResult()
        {
            var command = typeof(object);
            Func<CommandEnvelope, CancellationToken, Task> handler = (e, ct) => Task.CompletedTask;
            var sut = new CommandHandler(command, handler);

            Assert.Equal(command, sut.Command);
            Assert.Same(handler, sut.Handler);
        }
    }
}