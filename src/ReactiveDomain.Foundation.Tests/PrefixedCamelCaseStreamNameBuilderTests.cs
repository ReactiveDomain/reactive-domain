using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

[Collection(nameof(EmbeddedStreamStoreConnectionCollection))]
public sealed class PrefixedCamelCaseStreamNameBuilderTests {
	[Theory]
	[InlineData("")]
	[InlineData("  ")]
	[InlineData(null)]
	public void ThrowsOnNonExplicitNullOrEmptyPrefix(string? prefix) {
		Assert.Throws<ArgumentException>(() => new PrefixedCamelCaseStreamNameBuilder(prefix!));
	}

	[Fact]
	public void CanGeneratePrefixedCamelCaseStreamNameForAggregate() {
		var aggregateId = Guid.Parse("96370d8277ae4ccab626091775ed01bb");

		Assert.Equal(
			"testAggregate-96370d8277ae4ccab626091775ed01bb",
			new PrefixedCamelCaseStreamNameBuilder()
				.GenerateForAggregate(typeof(TestAggregate), aggregateId));

		Assert.Equal(
			"unittest.testAggregate-96370d8277ae4ccab626091775ed01bb",
			new PrefixedCamelCaseStreamNameBuilder("UnitTest")
				.GenerateForAggregate(typeof(TestAggregate), aggregateId));
	}

	[Fact]
	public void CanGenerateStreamNameForCategory() {
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
	public void CanGenerateStreamNameForEventType() {
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
