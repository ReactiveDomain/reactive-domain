using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ReactiveDomain
{
    public abstract class CommandHandlerModule : IEnumerable<CommandHandler>
    {
        private readonly List<CommandHandler> _handlers;

        protected CommandHandlerModule()
        {
            _handlers = new List<CommandHandler>();
        }

        protected CommandHandlerBuilder<TCommand> For<TCommand>() 
        {
            return new CommandHandlerBuilder<TCommand>(handler =>
            {
                _handlers.Add(
                    new CommandHandler(
                        typeof(TCommand),
                        (envelope, token) => handler(envelope.TypedAs<TCommand>(), token)
                    ));
            });
        }

        protected void Handle<TCommand>(Func<CommandEnvelope<TCommand>, Task> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _handlers.Add(
                new CommandHandler(
                    typeof(TCommand),
                    (envelope, token) => handler(envelope.TypedAs<TCommand>())
                )
            );
        }

        protected void Handle<TCommand>(Func<CommandEnvelope<TCommand>, CancellationToken, Task> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _handlers.Add(
                new CommandHandler(
                    typeof(TCommand),
                    (envelope, token) => handler(envelope.TypedAs<TCommand>(), token)
                )
            );
        }

        public CommandHandler[] Handlers => _handlers.ToArray();

        public CommandHandlerEnumerator GetEnumerator()
        {
            return new CommandHandlerEnumerator(Handlers);
        }

        IEnumerator<CommandHandler> IEnumerable<CommandHandler>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}