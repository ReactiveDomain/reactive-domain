using System.Collections.Generic;
using Albedo;
using AutoFixture;
using AutoFixture.Idioms;
using ReactiveDomain;
using Xunit;

namespace ReactiveDomain
{
    public class MetadataTests
    {
        private readonly Fixture _fixture;

        public MetadataTests()
        {
            _fixture = new Fixture();
        }

        [Fact]
        public void IsEnumerableOfMetadatum()
        {
            var sut = Metadata.None;
            Assert.IsAssignableFrom<IEnumerable<Metadatum>>(sut);
        }

        [Fact]
        public void MetadataCanNotBeNull()
        {
            new GuardClauseAssertion(_fixture)
                .Verify(Constructors.Select(() => new Metadata(null)));
        }

        [Fact]
        public void NoneReturnsExpectedResult()
        {
            var sut = Metadata.None;
            Assert.Equal(new Metadata(new Metadatum[0]), sut);
        }

        [Fact]
        public void ToKeyValuePairsReturnsExpectedResult()
        {
            var metadata = new[]
            {
                new Metadatum("name1", "value1"),
                new Metadatum("name2", "value2")
            };
            var result = new Metadata(metadata).ToKeyValuePairs();
            Assert.Equal(new []
            {
                new KeyValuePair<string, string>("name1", "value1"),
                new KeyValuePair<string, string>("name2", "value2"),
            }, result);
        }

        [Fact]
        public void WithPreservesMetadata()
        {
            var metadata = new[]
            {
                new Metadatum("name1", "value1"),
                new Metadatum("name2", "value2")
            };
            var sut = new Metadata(metadata);
            var result = sut.With(new Metadatum("name3", "value3")).ToKeyValuePairs();
            Assert.Equal(new[]
            {
                new KeyValuePair<string, string>("name1", "value1"),
                new KeyValuePair<string, string>("name2", "value2"),
                new KeyValuePair<string, string>("name3", "value3")
            }, result);
        }

        [Fact]
        public void WithNameAndValueGuardsNameCanNotBeNull()
        {
            new GuardClauseAssertion(_fixture)
                .Verify(new Methods<Metadata>().Select(_ => _.With(null, "value")));
        }

        [Fact]
        public void WithNameAndValueGuardsValueCanNotBeNull()
        {
            new GuardClauseAssertion(_fixture)
                .Verify(new Methods<Metadata>().Select(_ => _.With("name", null)));
        }

        [Fact]
        public void WithNameAndValuePreservesMetadata()
        {
            var metadata = new[]
            {
                new Metadatum("name1", "value1"),
                new Metadatum("name2", "value2")
            };
            var sut = new Metadata(metadata);
            var result = sut.With("name3", "value3").ToKeyValuePairs();
            Assert.Equal(new[]
            {
                new KeyValuePair<string, string>("name1", "value1"),
                new KeyValuePair<string, string>("name2", "value2"),
                new KeyValuePair<string, string>("name3", "value3")
            }, result);
        }

        [Fact]
        public void WithManyGuardsMetadataCanNotBeNull()
        {
            new GuardClauseAssertion(_fixture)
                .Verify(new Methods<Metadata>().Select(_ => _.With(null)));
        }

        [Fact]
        public void WithManyPreservesMetadata()
        {
            var metadata = new[]
            {
                new Metadatum("name1", "value1"),
                new Metadatum("name2", "value2")
            };
            var range = new[]
            {
                new Metadatum("name3", "value3"),
                new Metadatum("name4", "value4")
            };
            var sut = new Metadata(metadata);
            var result = sut.With(range).ToKeyValuePairs();
            Assert.Equal(new[]
            {
                new KeyValuePair<string, string>("name1", "value1"),
                new KeyValuePair<string, string>("name2", "value2"),
                new KeyValuePair<string, string>("name3", "value3"),
                new KeyValuePair<string, string>("name4", "value4")
            }, result);
        }

        [Fact]
        public void WithoutPreservesMetadata()
        {
            var metadata = new[]
            {
                new Metadatum("name1", "value1"),
                new Metadatum("name2", "value2")
            };
            var sut = new Metadata(metadata);
            var result = sut.Without(new Metadatum("name1", "value1")).ToKeyValuePairs();
            Assert.Equal(new[]
            {
                new KeyValuePair<string, string>("name2", "value2")
            }, result);
        }
        
        [Fact]
        public void WithoutGuardsNameCanNotBeNull()
        {
            new GuardClauseAssertion(_fixture)
                .Verify(new Methods<Metadata>().Select(_ => _.Without((string)null)));
        }

        [Fact]
        public void WithoutAnyMatchingNamesHasExpectedResult()
        {
            var metadata = new[]
            {
                new Metadatum("name1", "value1"),
                new Metadatum("name2", "value2")
            };
            var sut = new Metadata(metadata);
            var result = sut.Without("name3").ToKeyValuePairs();
            Assert.Equal(new[]
            {
                new KeyValuePair<string, string>("name1", "value1"),
                new KeyValuePair<string, string>("name2", "value2")
            }, result);
        }

        [Fact]
        public void WithoutManyMatchingNamesHasExpectedResult()
        {
            var metadata = new[]
            {
                new Metadatum("name1", "value1"),
                new Metadatum("name2", "value2"),
                new Metadatum("name1", "value3"),
            };
            var sut = new Metadata(metadata);
            var result = sut.Without("name1").ToKeyValuePairs();
            Assert.Equal(new[]
            {
                new KeyValuePair<string, string>("name2", "value2")
            }, result);
        }

        [Fact]
        public void WithoutOneMatchingNameHasExpectedResult()
        {
            var metadata = new[]
            {
                new Metadatum("name1", "value1"),
                new Metadatum("name2", "value2"),
                new Metadatum("name3", "value3"),
            };
            var sut = new Metadata(metadata);
            var result = sut.Without("name1").ToKeyValuePairs();
            Assert.Equal(new[]
            {
                new KeyValuePair<string, string>("name2", "value2"),
                new KeyValuePair<string, string>("name3", "value3")
            }, result);
        }

        [Fact]
        public void WithoutManyGuardsMetadataCanNotBeNull()
        {
            new GuardClauseAssertion(_fixture)
                .Verify(new Methods<Metadata>().Select(_ => _.Without((Metadatum[])null)));
        }

        [Fact]
        public void WithoutManyPreservesMetadata()
        {
            var metadata = new[]
            {
                new Metadatum("name1", "value1"),
                new Metadatum("name2", "value2"),
                new Metadatum("name3", "value3"),
                new Metadatum("name4", "value4")
            };
            var range = new[]
            {
                new Metadatum("name1", "value1"),
                new Metadatum("name4", "value4")
            };
            var sut = new Metadata(metadata);
            var result = sut.Without(range).ToKeyValuePairs();
            Assert.Equal(new[]
            {
                new KeyValuePair<string, string>("name2", "value2"),
                new KeyValuePair<string, string>("name3", "value3")
            }, result);
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
                .Verify(typeof(Metadata));
        }
    }
}