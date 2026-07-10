using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

// n.b a copy of this marker class must be in the assembly with the test classes
// copy and place in the local namespace to avoid collisions. This is not currently used,
// but is useful for the "Common" tests if we want to use StreamStoreConnectionFixture
// in LIVE_ES_CONNECTION mode.
[CollectionDefinition(nameof(EmbeddedStreamStoreConnectionCollection))]
public class EmbeddedStreamStoreConnectionCollection : ICollectionFixture<StreamStoreConnectionFixture>;
