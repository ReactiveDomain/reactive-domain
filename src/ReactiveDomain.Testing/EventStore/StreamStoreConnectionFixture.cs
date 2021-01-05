//#define LIVE_ES_CONNECTION

using System;
using System.Diagnostics;
using System.Threading;
using ReactiveDomain.Util;
using ReactiveDomain.EventStore;
using EventStore.ClientAPI;
#if (NET48)
using EventStore.ClientAPI.Embedded;
using EventStore.Common.Options;
#endif

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Testing {
    public class StreamStoreConnectionFixture : IDisposable {
        private static readonly TimeSpan TimeToStop = TimeSpan.FromSeconds(5);
        
        private readonly IDisposable _node = null;

        public StreamStoreConnectionFixture()
        {
            AdminCredentials = new UserCredentials("admin", "changeit");
#if LIVE_ES_CONNECTION
            Connection =new EventStoreConnectionWrapper(
                              EventStoreConnection.Create("ConnectTo=tcp://admin:changeit@localhost:1113; HeartBeatTimeout=500"));

            return;
#endif

#if !(NET48)

            Connection = new ReactiveDomain.Testing.EventStore.MockStreamStoreConnection("Test Fixture");
            Connection.Connect();
#else
            
            var node = EmbeddedVNodeBuilder
                        .AsSingleNode()
                        .OnDefaultEndpoints()
                        .RunInMemory()
                        .DisableDnsDiscovery()
                        .DisableHTTPCaching()
                        //.DisableScavengeMerging()
                        .DoNotVerifyDbHashes()
                        .RunProjections(ProjectionType.System)
                        .StartStandardProjections()
                        .Build();

            node.StartAndWaitUntilReady().Wait();
            Connection = new EventStoreConnectionWrapper(EmbeddedEventStoreConnection.Create(node));

            _node = new Disposer(() => {
                if (!node.Stop(TimeToStop, true, true)) {
                    Trace.WriteLine(
                        $"Failed to gracefully shut down the embedded Eventstore within {TimeToStop.TotalMilliseconds}ms.");
                }
                return Unit.Default;
            });
#endif
        }

        public IStreamStoreConnection Connection { get; }

        public UserCredentials AdminCredentials { get; }
        
        private bool _disposed;
        public void Dispose() {
            if (_disposed) return;
            Connection?.Close();
            Connection?.Dispose();
            _node?.Dispose();
            _disposed = true;
        }
    }
}