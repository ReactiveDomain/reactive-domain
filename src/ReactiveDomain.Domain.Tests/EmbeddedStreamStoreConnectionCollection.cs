using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Domain.Tests
{
    //n.b a copy of this marker class must be in the assembly with the test classes
    //copy and place in the local namespace
    [CollectionDefinition(nameof(EmbeddedStreamStoreConnectionCollection))]
    public class EmbeddedStreamStoreConnectionCollection : ICollectionFixture<StreamStoreConnectionFixture>
    {
    }
}