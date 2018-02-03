using System;
using System.Linq;
using System.Security.Claims;
using Xunit;

namespace ReactiveDomain.Domain.Tests
{
    public class CommandEnvelopeTests
    {
        public CommandEnvelopeTests()
        {
            CommandId = Guid.NewGuid();
            CorrelationId = Guid.NewGuid();
            SourceId = Guid.NewGuid();
            Command = new object();
            Principal = new ClaimsPrincipal();
            Metadata = ReactiveDomain.Metadata.None.With(
                Enumerable
                    .Range(0, new Random().Next(0, 3))
                    .Select(index => new Metadatum("key" + index, "value" + index))
                    .ToArray());
        }

        public Guid CommandId { get; }
        public Guid CorrelationId { get; }
        public Guid SourceId { get; }
        public object Command { get; }
        public ClaimsPrincipal Principal { get; }
        public Metadata Metadata { get; }

        [Fact]
        public void NewReturnsExpectedInstance()
        {
            var sut = new CommandEnvelope();
            Assert.Null(sut.Command);
            Assert.Equal(Guid.Empty, sut.CommandId);
            Assert.Equal(Guid.Empty, sut.CorrelationId);
            Assert.Null(sut.SourceId);
            Assert.Null(sut.Principal);
            Assert.Same(ReactiveDomain.Metadata.None, sut.Metadata);
        }

        [Fact]
        public void SetCommandReturnsExpectedInstance()
        {
            var sut = new CommandEnvelope().SetCommand(Command);
            Assert.Same(Command, sut.Command);

            Assert.Equal(Guid.Empty, sut.CommandId);
            Assert.Equal(Guid.Empty, sut.CorrelationId);
            Assert.Null(sut.SourceId);
            Assert.Null(sut.Principal);
            Assert.Same(ReactiveDomain.Metadata.None, sut.Metadata);
        }

        [Fact]
        public void SetCommandIdReturnsExpectedInstance()
        {
            var sut = new CommandEnvelope().SetCommandId(CommandId);
            Assert.Equal(CommandId, sut.CommandId);

            Assert.Null(sut.Command);
            Assert.Equal(Guid.Empty, sut.CorrelationId);
            Assert.Null(sut.SourceId);
            Assert.Null(sut.Principal);
            Assert.Same(ReactiveDomain.Metadata.None, sut.Metadata);
        }

        [Fact]
        public void SetCorrelationIdReturnsExpectedInstance()
        {
            var sut = new CommandEnvelope().SetCorrelationId(CorrelationId);
            Assert.Equal(CorrelationId, sut.CorrelationId);

            Assert.Null(sut.Command);
            Assert.Equal(Guid.Empty, sut.CommandId);
            Assert.Null(sut.SourceId);
            Assert.Null(sut.Principal);
            Assert.Same(ReactiveDomain.Metadata.None, sut.Metadata);
        }

        [Fact]
        public void SetSourceIdReturnsExpectedInstance()
        {
            var sut = new CommandEnvelope().SetSourceId(SourceId);
            Assert.Equal(SourceId, sut.SourceId);

            Assert.Null(sut.Command);
            Assert.Equal(Guid.Empty, sut.CommandId);
            Assert.Equal(Guid.Empty, sut.CorrelationId);
            Assert.Null(sut.Principal);
            Assert.Same(ReactiveDomain.Metadata.None, sut.Metadata);
        }

        [Fact]
        public void SetSourceIdToNullReturnsExpectedInstance()
        {
            var sut = new CommandEnvelope().SetSourceId(null);
            Assert.Null(sut.SourceId);

            Assert.Null(sut.Command);
            Assert.Equal(Guid.Empty, sut.CommandId);
            Assert.Equal(Guid.Empty, sut.CorrelationId);
            Assert.Null(sut.Principal);
            Assert.Same(ReactiveDomain.Metadata.None, sut.Metadata);
        }

        [Fact]
        public void SetMetadataReturnsExpectedInstance()
        {
            var sut = new CommandEnvelope().SetMetadata(Metadata);
            Assert.Same(Metadata, sut.Metadata);

            Assert.Null(sut.Command);
            Assert.Equal(Guid.Empty, sut.CommandId);
            Assert.Equal(Guid.Empty, sut.CorrelationId);
            Assert.Null(sut.Principal);
            Assert.Null(sut.SourceId);
        }

        [Fact]
        public void SetPrincipalReturnsExpectedInstance()
        {
            var sut = new CommandEnvelope().SetPrincipal(Principal);
            Assert.Same(Principal, sut.Principal);

            Assert.Null(sut.Command);
            Assert.Equal(Guid.Empty, sut.CommandId);
            Assert.Equal(Guid.Empty, sut.CorrelationId);
            Assert.Null(sut.SourceId);
            Assert.Same(ReactiveDomain.Metadata.None, sut.Metadata);
        }

        [Fact]
        public void TypedAsReturnsExpectedInstance()
        {
            var message = new Message();
            var sut = new CommandEnvelope()
                .SetCommand(message)
                .SetCommandId(CommandId)
                .SetCorrelationId(CorrelationId)
                .SetSourceId(SourceId)
                .SetMetadata(Metadata)
                .SetPrincipal(Principal);

            var result = sut.TypedAs<Message>();
            
            Assert.IsType<CommandEnvelope<Message>>(result);
            Assert.Same(message, result.Command);
            Assert.Equal(CommandId, result.CommandId);
            Assert.Equal(CorrelationId, result.CorrelationId);
            Assert.Equal(SourceId, result.SourceId);
            Assert.Same(Metadata, result.Metadata);
            Assert.Same(Principal, result.Principal);
        }

        [Fact]
        public void TypedAsThrowsIfCommandIsNotAssignableToType()
        {
            var sut = new CommandEnvelope()
                .SetCommand(Command)
                .SetCommandId(CommandId)
                .SetCorrelationId(CorrelationId)
                .SetSourceId(SourceId)
                .SetMetadata(Metadata)
                .SetPrincipal(Principal);

            Assert.Throws<InvalidCastException>(() => sut.TypedAs<Message>());
        }

        [Fact]
        public void TypedAsDoesNotThrowIfCommandIsAssignableToType()
        {
            var message = new Message();
            var sut = new CommandEnvelope()
                .SetCommand(message)
                .SetCommandId(CommandId)
                .SetCorrelationId(CorrelationId)
                .SetSourceId(SourceId)
                .SetMetadata(Metadata)
                .SetPrincipal(Principal);

            var result = sut.TypedAs<object>();
            
            Assert.IsType<CommandEnvelope<object>>(result);
            Assert.Same(message, result.Command);
            Assert.Equal(CommandId, result.CommandId);
            Assert.Equal(CorrelationId, result.CorrelationId);
            Assert.Equal(SourceId, result.SourceId);
            Assert.Same(Metadata, result.Metadata);
            Assert.Same(Principal, result.Principal);
        }
        
        private class Message {}
    }
}