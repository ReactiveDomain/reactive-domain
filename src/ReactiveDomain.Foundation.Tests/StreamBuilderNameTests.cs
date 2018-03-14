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
            Assert.Throws<ArgumentException>(() => new StreamNameBuilder(string.Empty));
            Assert.Throws<ArgumentException>(() => new StreamNameBuilder("  "));
            Assert.Throws<ArgumentException>(() => new StreamNameBuilder(null));
        }
    }
}