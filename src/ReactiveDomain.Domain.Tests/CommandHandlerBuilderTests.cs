using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ReactiveDomain.Domain.Tests
{
    public class CommandHandlerBuilderTests
    {
        private ForModule Module { get; }

        public CommandHandlerBuilderTests()
        {
            Module = new ForModule();
        }

        private class ForModule : CommandHandlerModule
        {
            public CommandHandlerBuilder<TCommand> RevealFor<TCommand>()
            {
                return For<TCommand>();
            }
        }

        [Fact]
        public void PipesPipeCanNotBeNull()
        {
            var sut = Module.RevealFor<object>();
            Assert.Throws<ArgumentNullException>(() => sut.Pipe(null));
        }

        [Fact]
        public void PipeReturnsExpectedResult()
        {
            var sut = Module.RevealFor<object>();

            var result = sut.Pipe(next => (e, ct) => Task.CompletedTask);

            Assert.IsType<CommandHandlerBuilder<object>>(result);
            Assert.NotSame(sut, result);
        }

        [Fact]
        public void HandlesHandlerCanNotBeNull()
        {
            var sut = Module.RevealFor<object>();

            Assert.Throws<ArgumentNullException>(() => sut.Handle(null));
        }

        [Fact]
        public void PipeDoesNotAutoRegisterInModule()
        {
            var sut = Module.RevealFor<object>();

            sut.Pipe(next => (e, ct) => Task.CompletedTask);

            Assert.Empty(Module.Handlers);
        }

        [Fact]
        public void HandleDoesAutoRegisterInModule()
        {
            var sut = Module.RevealFor<object>();

            sut.Handle((e, ct) => Task.CompletedTask);

            Assert.NotEmpty(Module.Handlers);
        }

        [Fact]
        public void PipeOrderIsPreserved()
        {
            Module
                .RevealFor<object>()
                .Pipe(next => 
                    (e, ct) => 
                    {
                        Assert.Equal(1, GetStep(e.Metadata));
                        return next(e.SetMetadata(SetStep(e.Metadata, 2)), ct);
                    })
                .Pipe(next => 
                    (e, ct) => 
                    {
                        Assert.Equal(2, GetStep(e.Metadata));
                        return next(e.SetMetadata(SetStep(e.Metadata, 3)), ct);
                    })
                .Handle(
                    (e, ct) => 
                    {
                        Assert.Equal(3, GetStep(e.Metadata));
                        return Task.CompletedTask;
                    });
            
            var envelope = new CommandEnvelope()
                .SetCommand(new object())
                .SetMetadata(Metadata.None.With(new Metadatum("step", "1")));

            new CommandHandlerInvoker(Module).Invoke(envelope);
        }

        private static int GetStep(Metadata metadata)
        {
            return Convert.ToInt32(Enumerable.Single<Metadatum>(metadata, metadatum => metadatum.Name == "step").Value);
        }

        private static Metadata SetStep(Metadata metadata, int value)
        {
            return new Metadata(
                Enumerable.Where<Metadatum>(metadata, metadatum => metadatum.Name != "step")
                    .Concat(new [] { new Metadatum("step", value.ToString()) })
                    .ToArray()
            );
        }
    }
}