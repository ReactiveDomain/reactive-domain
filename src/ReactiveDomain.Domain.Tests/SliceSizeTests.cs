using System;
using AutoFixture;
using AutoFixture.Idioms;
using Xunit;

namespace ReactiveDomain
{
    public class SliceSizeTests
    {
        private readonly Fixture _fixture;

        public SliceSizeTests()
        {
            _fixture = new Fixture();
        }

        [Theory]
        [InlineData(int.MinValue)]
        [InlineData(-1)]
        [InlineData(0)]
        public void SizeCanNotBeOutOfBounds(int value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SliceSize(value));
        }

        [Theory]
        [InlineData(int.MaxValue)]
        [InlineData(1)]
        public void SizeCanBeWithinBounds(int value)
        {
            var _ = new SliceSize(value);
        }

        [Fact]
        public void CanImplicitlyBeConvertedToInt32()
        {
            Int32 value = new SliceSize(2);
            Assert.Equal(2, value);
        }

        [Fact]
        public void CanBeConvertedToInt32()
        {
            var value = new SliceSize(3).ToInt32();
            Assert.Equal(3, value);
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
                .Verify(typeof(SliceSize));
        }
    }
}
