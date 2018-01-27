using System.Collections.Generic;
using Albedo;
using AutoFixture;
using AutoFixture.Idioms;
using Xunit;

namespace ReactiveDomain
{
    namespace StreamNameTests
    {
        public class DefaultInstance
        {
            private readonly Fixture _fixture;
            private readonly StreamName _sut;

            public DefaultInstance()
            {
                _fixture = new Fixture();
                _sut = default(StreamName);
            }

            [Fact]
            public void ToStringReturnsExpectedResult()
            {
                Assert.Equal("", _sut.ToString());
            }

            [Fact]
            public void EndsWithReturnsExpectedResult()
            {
                Assert.False(_sut.EndsWith(_fixture.Create<string>()));
            }

            [Fact]
            public void StartsWithReturnsExpectedResult()
            {
                Assert.False(_sut.StartsWith(_fixture.Create<string>()));
            }

            [Fact]
            public void WithPrefixReturnsExpectedResult()
            {
                var prefix = _fixture.Create<string>();
                var result = _sut.WithPrefix(prefix);
                Assert.Equal(new StreamName(prefix), result);
            }

            [Fact]
            public void WithoutPrefixReturnsExpectedResult()
            {
                var prefix = _fixture.Create<string>();
                var result = _sut.WithoutPrefix(prefix);
                Assert.Equal(_sut, result);
            }

            [Fact]
            public void WithSuffixReturnsExpectedResult()
            {
                var suffix = _fixture.Create<string>();
                var result = _sut.WithSuffix(suffix);
                Assert.Equal(new StreamName(suffix), result);
            }

            [Fact]
            public void WithoutSuffixReturnsExpectedResult()
            {
                var suffix = _fixture.Create<string>();
                var result = _sut.WithoutSuffix(suffix);
                Assert.Equal(_sut, result);
            }

            [Fact]
            public void CanImplicitlyBeConvertedToString()
            {
                string result = _sut;
                Assert.Equal("", result);
            }
        }

        public class AnyInstance
        {
            private readonly Fixture _fixture;

            public AnyInstance()
            {
                _fixture = new Fixture();
            }

            [Fact]
            public void NameCanNotBeNull()
            {
                new GuardClauseAssertion(_fixture)
                    .Verify(Constructors.Select(() => new StreamName(null)));
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
                    .Verify(typeof(StreamName));
            }
        }

        public class NamedInstance
        {
            private readonly Fixture _fixture;
            private readonly string _name;
            private readonly StreamName _sut;

            public NamedInstance()
            {
                _fixture = new Fixture();
                _name = _fixture.Create<string>();
                _sut = new StreamName(_name);
            }

            [Fact]
            public void ToStringReturnsExpectedResult()
            {
                Assert.Equal(_name, _sut.ToString());
            }

            [Theory]
            [MemberData(nameof(EndsWithCases))]
            public void EndsWithReturnsExpectedResult(string name, string suffix, bool expected)
            {
                var sut = new StreamName(name);
                var result = sut.EndsWith(suffix);
                Assert.Equal(expected, result);
            }

            public static IEnumerable<object[]> EndsWithCases
            {
                get
                {
                    yield return new object[] { "", "", true};
                    yield return new object[] { "", "a", false };
                    yield return new object[] { "a", "", true };
                    yield return new object[] { "a", "a", true };

                    yield return new object[] { "ab", "", true };
                    yield return new object[] { "ab", "abc", false };
                    yield return new object[] { "ab", "ab", true };
                    yield return new object[] { "ab", "b", true };
                }
            }

            [Theory]
            [MemberData(nameof(StartsWithCases))]
            public void StartsWithReturnsExpectedResult(string name, string prefix, bool expected)
            {
                var sut = new StreamName(name);
                var result = sut.StartsWith(prefix);
                Assert.Equal(expected, result);
            }

            public static IEnumerable<object[]> StartsWithCases
            {
                get
                {
                    yield return new object[] { "", "", true };
                    yield return new object[] { "", "a", false };
                    yield return new object[] { "a", "", true };
                    yield return new object[] { "a", "a", true };

                    yield return new object[] { "ab", "", true };
                    yield return new object[] { "ab", "abc", false };
                    yield return new object[] { "ab", "ab", true };
                    yield return new object[] { "ab", "a", true };
                }
            }

            [Fact]
            public void WithPrefixReturnsExpectedResult()
            {
                var prefix = _fixture.Create<string>();
                var result = _sut.WithPrefix(prefix);
                Assert.Equal(new StreamName(prefix + _name), result);
            }

            [Theory]
            [InlineData("a", "", "a")]
            [InlineData("a", "a", "")]
            [InlineData("ab", "a", "b")]
            [InlineData("ab", "b", "ab")]
            [InlineData("ab", "ab", "")]
            [InlineData("ab", "abc", "ab")]
            public void WithoutPrefixReturnsExpectedResult(string name, string prefix, string expected)
            {
                var sut = new StreamName(name);
                var result = sut.WithoutPrefix(prefix);
                Assert.Equal(new StreamName(expected), result);
            }

            [Fact]
            public void WithSuffixReturnsExpectedResult()
            {
                var suffix = _fixture.Create<string>();
                var result = _sut.WithSuffix(suffix);
                Assert.Equal(new StreamName(_name + suffix), result);
            }

            [Theory]
            [InlineData("a", "", "a")]
            [InlineData("a", "a", "")]
            [InlineData("ab", "a", "ab")]
            [InlineData("ab", "b", "a")]
            [InlineData("ab", "ab", "")]
            [InlineData("ab", "abc", "ab")]
            public void WithoutSuffixReturnsExpectedResult(string name, string suffix, string expected)
            {
                var sut = new StreamName(name);
                var result = sut.WithoutSuffix(suffix);
                Assert.Equal(new StreamName(expected), result);
            }

            [Fact]
            public void CanImplicitlyBeConvertedToString()
            {
                string result = _sut;
                Assert.Equal(_name, result);
            }
        }
    }
}
