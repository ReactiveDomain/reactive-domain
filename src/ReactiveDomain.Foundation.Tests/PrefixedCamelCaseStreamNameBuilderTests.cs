
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

            Assert.Equal(
                "testAggregate-96370d8277ae4ccab626091775ed01bb",
                new PrefixedCamelCaseStreamNameBuilder()
                    .GenerateForAggregate(typeof(TestAggregate), aggregareId));

            Assert.Equal(
                "unittest.testAggregate-96370d8277ae4ccab626091775ed01bb",
                new PrefixedCamelCaseStreamNameBuilder("UnitTest")
                    .GenerateForAggregate(typeof(TestAggregate), aggregareId));
        }

        [Fact]
        public void CanGenerateStreamNameForCategory()
        {
            Assert.Equal(
                "$ce-testAggregate",
                new PrefixedCamelCaseStreamNameBuilder()
                    .GenerateForCategory(typeof(TestAggregate)));

            Assert.Equal(
                "$ce-unittest.testAggregate",
                new PrefixedCamelCaseStreamNameBuilder("UnitTest")
                    .GenerateForCategory(typeof(TestAggregate)));
        }

        [Fact]
        public void CanGenerateStreamNameForEventType()
        {
            Assert.Equal(
                "$et-TestEventType", 
                new PrefixedCamelCaseStreamNameBuilder("whateverThePrefixIs")
                    .GenerateForEventType("TestEventType"));

            Assert.Equal(
                "$et-TestEventType", 
                new PrefixedCamelCaseStreamNameBuilder()
                    .GenerateForEventType("TestEventType"));
        }
    }
}