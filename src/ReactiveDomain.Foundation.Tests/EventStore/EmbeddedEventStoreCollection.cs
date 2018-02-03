using Xunit;

namespace ReactiveDomain.Foundation.Tests.EventStore
{
    [CollectionDefinition("ESEmbeded")]
    public class EmbeddedEventStoreCollection : ICollectionFixture<Testing.EventStore.EmbeddedEventStoreFixture>
    {
    }
}
