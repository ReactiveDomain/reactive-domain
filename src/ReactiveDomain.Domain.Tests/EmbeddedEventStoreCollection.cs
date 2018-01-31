using Xunit;

namespace ReactiveDomain
{
    [CollectionDefinition(nameof(EmbeddedEventStoreCollection))]
    public class EmbeddedEventStoreCollection : ICollectionFixture<EmbeddedEventStoreFixture>
    {
    }
}