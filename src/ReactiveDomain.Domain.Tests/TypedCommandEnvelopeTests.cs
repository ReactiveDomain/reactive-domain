using System;
using System.Linq;
using System.Security.Claims;
using Xunit;

namespace ReactiveDomain.Domain.Tests
{
    public class TypedCommandEnvelopeTests
    {
        public TypedCommandEnvelopeTests()
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

        private CommandEnvelope<object> CreateSut()
        {
            return new CommandEnvelope().TypedAs<object>();
        }

        [Fact]
        public void SetCommandReturnsExpectedInstance()
        {
            var command = new object();
            var sut = CreateSut().SetCommand(command);
            Assert.Same(command, sut.Command);

            Assert.Equal(Guid.Empty, sut.CommandId);
            Assert.Equal(Guid.Empty, sut.CorrelationId);
            Assert.Null(sut.SourceId);
            Assert.Null(sut.Principal);
            Assert.Same(ReactiveDomain.Metadata.None, sut.Metadata);
        }

        [Fact]
        public void SetCommandIdReturnsExpectedInstance()
        {
            var sut = CreateSut().SetCommandId(CommandId);
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
            var sut = CreateSut().SetCorrelationId(CorrelationId);
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
            var sut = CreateSut().SetSourceId(SourceId);
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
            var sut = CreateSut().SetSourceId(null);
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
            var sut = CreateSut().SetMetadata(Metadata);
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
            var sut = CreateSut().SetPrincipal(Principal);
            Assert.Same(Principal, sut.Principal);

            Assert.Null(sut.Command);
            Assert.Equal(Guid.Empty, sut.CommandId);
            Assert.Equal(Guid.Empty, sut.CorrelationId);
            Assert.Null(sut.SourceId);
            Assert.Same(ReactiveDomain.Metadata.None, sut.Metadata);
        }
    }
}