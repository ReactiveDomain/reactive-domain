using ReactiveDomain.Testing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ReactiveDomain.Foundation.Tests
{
    [CollectionDefinition(nameof(EventStoreCollection))]
    public class EventStoreCollection : ICollectionFixture<EmbeddedEventStoreFixture>
    {
    }
}
