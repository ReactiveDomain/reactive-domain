using System.Collections.Generic;
using Albedo;
using AutoFixture;
using AutoFixture.Idioms;
using Xunit;

namespace ReactiveDomain.Domain.Tests
{
    public class MetadatumTests
    {
        private readonly Fixture _fixture;

        public MetadatumTests()
        {
            _fixture = new Fixture();
        }

        [Fact]
        public void BothNameAndValueCanNotBeNull()
        {
            new GuardClauseAssertion(_fixture).
                Verify(Constructors.Select(() => new Metadatum(null, null)));
        }

        [Fact]
        public void BothNameAndValueReturnExpectedResult()
        {
            new ConstructorInitializedMemberAssertion(_fixture).
                Verify(Constructors.Select(() => new Metadatum("name", "value")));
        }

        [Fact]
        public void ToKeyValuePairReturnsExpectedResult()
        {
            var sut = new Metadatum("name", "value");
            var result = sut.ToKeyValuePair();
            Assert.Equal(new KeyValuePair<string, string>("name", "value"), result);
        }

        [Fact]
        public void VerifyEquality()
        {
            new CompositeIdiomaticAssertion(
                    new EqualsNewObjectAssertion(_fixture),
                    new EqualsNullAssertion(_fixture),
                    new EqualsSelfAssertion(_fixture),
                    new GetHashCodeSuccessiveAssertion(_fixture),
                    new EqualsSuccessiveAssertion(_fixture))
                .Verify(typeof(Metadatum));
        }
    }
}
