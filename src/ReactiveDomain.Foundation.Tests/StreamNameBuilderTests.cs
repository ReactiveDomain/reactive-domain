using ReactiveDomain.Foundation.EventStore;
using System;
using Xunit;

namespace ReactiveDomain.Foundation.Tests
{
    [Collection(nameof(EventStoreCollection))]
    public class StreamNameBuilderTests
    {
        [Fact]
        public void ThrowsOnNonExplicitNullOrEmptyPrefix()
        {
            string emptyPrefix = string.Empty;
            string whiteSpacePrefix = " ";

            Assert.Throws<ArgumentException>(() => StreamNameBuilder.Generate(emptyPrefix, typeof(IEventSource), Guid.NewGuid()));
            Assert.Throws<ArgumentException>(() => StreamNameBuilder.Generate(whiteSpacePrefix, typeof(IEventSource), Guid.NewGuid()));
            Assert.Throws<ArgumentNullException>(() => StreamNameBuilder.Generate(null, typeof(IEventSource), Guid.NewGuid()));
        }
    }
}