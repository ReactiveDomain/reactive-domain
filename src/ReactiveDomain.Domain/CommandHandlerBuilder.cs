using System;
using System.Threading;
using System.Threading.Tasks;

namespace ReactiveDomain
{
    public class CommandHandlerBuilder<TCommand>
    {
        private readonly Action<Func<CommandEnvelope<TCommand>, CancellationToken, Task>> _build;
        private readonly Func<Func<CommandEnvelope<TCommand>, CancellationToken, Task>, Func<CommandEnvelope<TCommand>, CancellationToken, Task>>[] _pipeline;

        internal CommandHandlerBuilder(Action<Func<CommandEnvelope<TCommand>, CancellationToken, Task>> build)
            : this(build, new Func<Func<CommandEnvelope<TCommand>, CancellationToken, Task>, Func<CommandEnvelope<TCommand>, CancellationToken, Task>>[0])
        {
        }

        private CommandHandlerBuilder(Action<Func<CommandEnvelope<TCommand>, CancellationToken, Task>> build, Func<Func<CommandEnvelope<TCommand>, CancellationToken, Task>, Func<CommandEnvelope<TCommand>, CancellationToken, Task>>[] pipeline)
        {
            _build = build ?? throw new ArgumentNullException(nameof(build));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        }

        public CommandHandlerBuilder<TCommand> Pipe(Func<Func<CommandEnvelope<TCommand>, CancellationToken, Task>, Func<CommandEnvelope<TCommand>, CancellationToken, Task>> pipe)
        {
            if (pipe == null)
                throw new ArgumentNullException(nameof(pipe));

            var pipeline = new Func<
                Func<CommandEnvelope<TCommand>, CancellationToken, Task>,
                Func<CommandEnvelope<TCommand>, CancellationToken, Task>
            >[_pipeline.Length + 1];
            _pipeline.CopyTo(pipeline, 0);
            pipeline[_pipeline.Length] = pipe;

            return new CommandHandlerBuilder<TCommand>(_build, pipeline);
        }

        public void Handle(Func<CommandEnvelope<TCommand>, CancellationToken, Task> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var next = handler;
            var index = _pipeline.Length - 1;
            while (index >= 0)
            {
                var pipe = _pipeline[index];
                next = pipe(next);
                index--;
            }

            _build(next);
        }
    }
}