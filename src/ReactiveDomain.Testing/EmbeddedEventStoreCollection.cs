using Xunit;

namespace ReactiveDomain.Testing
{
    [CollectionDefinition(nameof(EmbeddedEventStoreCollection))]
    public class EmbeddedEventStoreCollection : ICollectionFixture<EmbeddedEventStoreFixture>
    {
    }
}