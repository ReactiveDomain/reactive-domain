using System;
using ReactiveDomain;
using Xunit;

namespace ReactiveDomain
{
    public class ReadResultTests
    {
        [Fact]
        public void NotFoundReturnsExpectedInstance()
        {
            var result = ReadResult.NotFound;

            Assert.Equal(ReadResultState.NotFound, result.State);
            Assert.Throws<InvalidOperationException>(() => result.Value);
        }

        [Fact]
        public void NotFoundEquality()
        {
            Assert.Equal(ReadResult.NotFound, ReadResult.NotFound);
            Assert.Equal(ReadResult.NotFound.GetHashCode(), ReadResult.NotFound.GetHashCode());
            Assert.NotEqual(ReadResult.NotFound, ReadResult.Deleted);
            Assert.NotEqual(ReadResult.NotFound.GetHashCode(), ReadResult.Deleted.GetHashCode());
            Assert.NotEqual(ReadResult.NotFound, ReadResult.Found(default(IEventSource)));
            Assert.NotEqual(ReadResult.NotFound.GetHashCode(), ReadResult.Found(default(IEventSource)).GetHashCode());
        }

        [Fact]
        public void DeletedReturnsExpectedInstance()
        {
            var result = ReadResult.Deleted;

            Assert.Equal(ReadResultState.Deleted, result.State);
            Assert.Throws<InvalidOperationException>(() => result.Value);
        }

        [Fact]
        public void DeletedEquality()
        {
            Assert.Equal(ReadResult.Deleted, ReadResult.Deleted);
            Assert.Equal(ReadResult.Deleted.GetHashCode(), ReadResult.Deleted.GetHashCode());
            Assert.NotEqual(ReadResult.Deleted, ReadResult.NotFound);
            Assert.NotEqual(ReadResult.Deleted.GetHashCode(), ReadResult.NotFound.GetHashCode());
            Assert.NotEqual(ReadResult.Deleted, ReadResult.Found(default(IEventSource)));
            Assert.NotEqual(ReadResult.Deleted.GetHashCode(), ReadResult.Found(default(IEventSource)).GetHashCode());
        }

        [Fact]
        public void FoundReturnsExpectedInstance()
        {
            var value = new Entity();
            var result = ReadResult.Found(value);

            Assert.Equal(ReadResultState.Found, result.State);
            Assert.Equal(value, result.Value);
        }
        
        [Fact]
        public void FoundEquality()
        {
            var value1 = new Entity();
            var sut1 = ReadResult.Found(value1);

            var value2 = new Entity();
            var sut2A = ReadResult.Found(value2);
            var sut2B = ReadResult.Found(value2);

            Assert.Equal(sut1, sut1);
            Assert.Equal(sut2A, sut2B);
            Assert.NotEqual(sut2A, sut1);
            Assert.NotEqual(sut2B, sut1);

            Assert.Equal(sut1.GetHashCode(), sut1.GetHashCode());
            Assert.Equal(sut2A.GetHashCode(), sut2B.GetHashCode());
            Assert.NotEqual(sut2A.GetHashCode(), sut1.GetHashCode());
            Assert.NotEqual(sut2B.GetHashCode(), sut1.GetHashCode());

            Assert.NotEqual(sut1, ReadResult.NotFound);
            Assert.NotEqual(sut1.GetHashCode(), ReadResult.NotFound.GetHashCode());
            Assert.NotEqual(sut1, ReadResult.Deleted);
            Assert.NotEqual(sut1.GetHashCode(), ReadResult.Deleted.GetHashCode());
        }

        class Entity : AggregateRootEntity { }
    }
}
