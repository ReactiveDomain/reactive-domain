using System;
using System.Threading;
using System.Threading.Tasks;

namespace ReactiveDomain
{
    public class CommandHandler
    {
        public Type Command { get; }
        public Func<CommandEnvelope, CancellationToken, Task> Handler { get; }

        public CommandHandler(Type command, Func<CommandEnvelope, CancellationToken, Task> handler)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }
    }
}