using System;
using System.Diagnostics;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Embedded;
using EventStore.Core;
using Xunit;

namespace ReactiveDomain.Foundation.Testing.EventStore
{
    [CollectionDefinition("ESEmbeded")]
    public class EsCollection : ICollectionFixture<EmbeddedEventStoreFixture>
    {
    }

    public class EmbeddedEventStoreFixture : IDisposable
    {
        private static readonly TimeSpan TimeToStop = TimeSpan.FromSeconds(5);
        private readonly ClusterVNode _node;

        public EmbeddedEventStoreFixture()
        {
            _node = EmbeddedVNodeBuilder
                .AsSingleNode()
                .OnDefaultEndpoints()
                .RunInMemory()
                .DisableDnsDiscovery()
                .DisableHTTPCaching()
                //.DisableScavengeMerging()
                .DoNotVerifyDbHashes()
                .Build();
            _node.StartAndWaitUntilReady().Wait();

            Connection = EmbeddedEventStoreConnection.Create(_node);
           // Connection = EventStoreConnection.Create("ConnectTo=tcp://admin:changeit@localhost:1113");
            Connection.ConnectAsync().Wait();
        }

        public IEventStoreConnection Connection { get; }

        public void Dispose()
        {
            Connection?.Close();
            if (_node == null) return;
            if (!_node.Stop(TimeToStop, true, true))
            {
                Trace.WriteLine($"Failed to gracefully shut down the embedded eventstore within {TimeToStop.TotalMilliseconds}ms.");
            }
        }
    }
}
