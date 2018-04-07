
using System;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests
{
    [Collection(nameof(EmbeddedStreamStoreConnectionCollection))]
    public class PrefixedCamelCaseStreamNameBuilderTests
    {
        [Fact]
        public void ThrowsOnNonExplicitNullOrEmptyPrefix()
        {
            Assert.Throws<ArgumentException>(() => new PrefixedCamelCaseStreamNameBuilder(string.Empty));
            Assert.Throws<ArgumentException>(() => new PrefixedCamelCaseStreamNameBuilder("  "));
            Assert.Throws<ArgumentException>(() => new PrefixedCamelCaseStreamNameBuilder(null));
        }

        [Fact]
        public void CanGeneratePrefixedCamelCaseStreamNameForAggregate()
        {
            var aggregareId = Guid.Parse("96370d8277ae4ccab626091775ed01bb");
            var prefix = "UnitTest";
            var streamNamebuilder = new PrefixedCamelCaseStreamNameBuilder(prefix);
            var streamName = streamNamebuilder.GenerateForAggregate(typeof(TestAggregate), aggregareId);

            Assert.Equal("unittest.testAggregate-96370d8277ae4ccab626091775ed01bb", streamName);
        }

        [Fact]
        public void CanGenerateStreamNameForCategory()
        {
            var streamNamebuilder = new PrefixedCamelCaseStreamNameBuilder();
            var streamName = streamNamebuilder.GenerateForCategory(typeof(TestAggregate));

            Assert.Equal("$ce-testAggregate", streamName);
        }

        [Fact]
        public void CanGenerateStreamNameForEventType()
        {
            var streamNamebuilder = new PrefixedCamelCaseStreamNameBuilder();
            var streamName = streamNamebuilder.GenerateForEventType("TestEventType");

            Assert.Equal("$et-testEventType", streamName);
        }
    }
}