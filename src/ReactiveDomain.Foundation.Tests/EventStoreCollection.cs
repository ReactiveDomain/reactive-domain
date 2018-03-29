using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests
{
    [CollectionDefinition(nameof(EventStoreCollection))]
    public class EventStoreCollection : ICollectionFixture<EmbeddedEventStoreFixture>
    {
    }
}
