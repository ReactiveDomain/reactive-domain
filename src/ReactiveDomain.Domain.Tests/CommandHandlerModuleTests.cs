using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ReactiveDomain
{
    public class CommandHandlerModuleTests
    {
        [Fact]
        public void IsEnumerableOfCommandHandler()
        {
            var sut = new EmptyModule();
            Assert.IsAssignableFrom<IEnumerable<CommandHandler>>(sut);
        }

        private class EmptyModule : CommandHandlerModule {}

        [Fact]
        public void GetEnumeratorReturnsExpectedResult()
        {
            var sut = new EmptyModule();
            Assert.IsType<CommandHandlerEnumerator>(sut.GetEnumerator());
        }

        [Fact]
        public void ForReturnsExpectedResult()
        {
            var sut = new ForModule();

            var result = sut.RevealFor<object>();

            Assert.IsType<CommandHandlerBuilder<object>>(result);
        }

        private class ForModule : CommandHandlerModule
        {
            public CommandHandlerBuilder<TCommand> RevealFor<TCommand>()
            {
                return For<TCommand>();
            }
        }

        [Fact]
        public void HandlesHandlerWithCancellationTokenCanNotBeNull()
        {
            var sut = new HandleWithCancellationTokenModule();
            
            Assert.Throws<ArgumentNullException>(() => sut.RevealHandle<object>(null));
        }

        private class HandleWithCancellationTokenModule : CommandHandlerModule
        {
            public void RevealHandle<TCommand>(Func<CommandEnvelope<TCommand>, CancellationToken, Task> handler)
            {
                Handle(handler);
            }
        }

        [Fact]
        public void HandlesHandlerWithoutCancellationTokenCanNotBeNull()
        {
            var sut = new HandleWithoutCancellationTokenModule();
            
            Assert.Throws<ArgumentNullException>(() => sut.RevealHandle<object>(null));
        }

        private class HandleWithoutCancellationTokenModule : CommandHandlerModule
        {
            public void RevealHandle<TCommand>(Func<CommandEnvelope<TCommand>, Task> handler)
            {
                Handle(handler);
            }
        }

        [Fact]
        public void HandlersOfEmptyModuleReturnsExpectedResult()
        {
            var sut = new EmptyModule();

            Assert.Empty(sut.Handlers);
        }

        [Fact]
        public async Task HandlersOfForModuleReturnsExpectedResult()
        {
            var sut = new ForModule();
            var signal = new Signal();

            sut.RevealFor<object>()
                .Pipe(next => (e, ct) => next(e,ct))
                .Pipe(next => (e, ct) => next(e,ct))
                .Handle((e, ct) => {
                    signal.Set();
                    return Task.CompletedTask;
                });

            var handler = sut.Handlers.Single();
            Assert.Equal(typeof(object), handler.Command);
            await handler.Handler(new CommandEnvelope().SetCommand(new object()), default(CancellationToken));
            Assert.True(signal.IsSet);
        }

        [Fact]
        public async Task HandlersOfHandleModuleWithoutCancellationTokenReturnsExpectedResult()
        {
            var sut = new HandleWithoutCancellationTokenModule();
            var signal = new Signal();

            sut.RevealHandle<object>(
                e => {
                    signal.Set();
                    return Task.CompletedTask;
                });

            var handler = sut.Handlers.Single();
            Assert.Equal(typeof(object), handler.Command);
            await handler.Handler(new CommandEnvelope().SetCommand(new object()), default(CancellationToken));
            Assert.True(signal.IsSet);
        }

        [Fact]
        public async Task HandlersOfHandleModuleWithCancellationTokenReturnsExpectedResult()
        {
            var sut = new HandleWithCancellationTokenModule();
            var signal = new Signal();

            sut.RevealHandle<object>(
                (e, ct) => {
                    signal.Set();
                    return Task.CompletedTask;
                });

            var handler = sut.Handlers.Single();
            Assert.Equal(typeof(object), handler.Command);
            await handler.Handler(new CommandEnvelope().SetCommand(new object()), default(CancellationToken));
            Assert.True(signal.IsSet);
        }

        [Fact]
        public async Task HandlersOfMixedModuleReturnsExpectedResult()
        {
            var sut = new MixedModule();
            var signal = new Signal();

            sut.RevealFor<Message1>()
                .Pipe(next => (e, ct) => next(e,ct))
                .Pipe(next => (e, ct) => next(e,ct))
                .Handle((e, ct) => {
                    signal.Set();
                    return Task.CompletedTask;
                });
            sut.RevealHandle<Message2>(
                e => {
                    signal.Set();
                    return Task.CompletedTask;
                });
            sut.RevealHandle<Message3>(
                (e, ct) => {
                    signal.Set();
                    return Task.CompletedTask;
                });

            Assert.Equal(3, sut.Handlers.Length);

            var handler1 = sut.Handlers[0];
            Assert.Equal(typeof(Message1), handler1.Command);
            await handler1.Handler(new CommandEnvelope().SetCommand(new Message1()), default(CancellationToken));
            Assert.True(signal.IsSet);

            signal.Reset();           

            var handler2 = sut.Handlers[1];
            Assert.Equal(typeof(Message2), handler2.Command);
            await handler2.Handler(new CommandEnvelope().SetCommand(new Message2()), default(CancellationToken));
            Assert.True(signal.IsSet);

            signal.Reset();

            var handler3 = sut.Handlers[2];
            Assert.Equal(typeof(Message3), handler3.Command);
            await handler3.Handler(new CommandEnvelope().SetCommand(new Message3()), default(CancellationToken));
            Assert.True(signal.IsSet);
        }

        private class MixedModule : CommandHandlerModule
        {
            public CommandHandlerBuilder<TCommand> RevealFor<TCommand>()
            {
                return For<TCommand>();
            }

            public void RevealHandle<TCommand>(Func<CommandEnvelope<TCommand>, Task> handler)
            {
                Handle(handler);
            }

            public void RevealHandle<TCommand>(Func<CommandEnvelope<TCommand>, CancellationToken, Task> handler)
            {
                Handle(handler);
            }
        }

        private class Message1 {}
        private class Message2 {}
        private class Message3 {}

        private class Signal 
        {
            public Signal()
            {
                IsSet = false;
            }
            
            public bool IsSet { get; private set; }

            public void Set() { IsSet = true; }

            public void Reset() { IsSet = false; }
        }
    }
}