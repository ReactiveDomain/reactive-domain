using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ReactiveDomain
{
    public class CommandHandlerInvoker
    {
        private readonly Dictionary<Type, Func<CommandEnvelope, CancellationToken, Task>> _routes;

        public CommandHandlerInvoker(CommandHandlerModule module)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));

            _routes = module
                .Handlers
                .ToDictionary(handler => handler.Command, handler => handler.Handler);
        }

        public CommandHandlerInvoker(CommandHandlerModule[] modules)
        {
            if (modules == null)
                throw new ArgumentNullException(nameof(modules));

            _routes = modules
                .SelectMany(module => module.Handlers)
                .ToDictionary(handler => handler.Command, handler => handler.Handler);
        }

        public Task Invoke(CommandEnvelope envelope, CancellationToken ct = default (CancellationToken))
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));

            if (_routes.TryGetValue(envelope.Command.GetType(), out Func<CommandEnvelope, CancellationToken, Task> handler))
            {
                return handler(envelope, ct);
            }
            throw new InvalidOperationException($"Command {envelope.Command.GetType().Name} could not be invoked because no matching handler was found.");
        }
    }
}