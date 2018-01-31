using System;
using System.Diagnostics;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Embedded;
using EventStore.Core;
#if NET462

#endif
#if NETCOREAPP2_0 || NETSTANDARD2_0
using System.Collections.Generic;
using System.Linq;
using EventStore.ClientAPI.SystemData;
using Docker.DotNet;
using Docker.DotNet.Models;
using System.Threading.Tasks;
using Xunit;
#endif

namespace ReactiveDomain.Domain.Tests
{
#if NET462
    public class EmbeddedEventStoreFixture : IDisposable
    {
        private static readonly TimeSpan TimeToStop = TimeSpan.FromSeconds(5);

        private int _suffix;
        private int _prefix;
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
        }

        public IEventStoreConnection Connection { get; }

        public StreamName NextStreamName()
        {
            return new StreamName($"stream-{Interlocked.Increment(ref _suffix)}");
        }

        public string NextStreamNamePrefix()
        {
            return $"scenario-{Interlocked.Increment(ref _prefix):D}-";
        }

        public void Dispose()
        {
            Connection?.Close();
            if (!_node.Stop(TimeToStop, true, true))
            {
                Trace.WriteLine($"Failed to gracefully shut down the embedded eventstore within {TimeToStop.TotalMilliseconds}ms.");
            }
        }
    }
#elif NETCOREAPP2_0 || NETSTANDARD2_0
    public class EmbeddedEventStoreFixture : IAsyncLifetime
    {
        private int _suffix;
        private int _prefix;

        public EmbeddedEventStoreFixture()
        {
            EventStoreContainer = "es" + Guid.NewGuid().ToString("N");
        }

        private string EventStoreContainer { get; set; }

        public StreamName NextStreamName()
        {
            return new StreamName($"stream-{Interlocked.Increment(ref _suffix)}");
        }

        public string NextStreamNamePrefix()
        {
            return $"scenario-{Interlocked.Increment(ref _prefix):D}-";
        }

        public IEventStoreConnection Connection { get; private set; }

        const string EventStoreImage = "eventstore/eventstore";

        public async Task InitializeAsync()
        {
            var address = Environment.OSVersion.Platform == PlatformID.Unix 
                ? new Uri("unix:///var/run/docker.sock")
                : new Uri("npipe://./pipe/docker_engine");
            var config = new DockerClientConfiguration(address);
            this.Client = config.CreateClient();
            var images = await this.Client.Images.ListImagesAsync(new ImagesListParameters { MatchName = EventStoreImage });
            if (images.Count == 0)
            {
                // No image found. Pulling latest ..
                Console.WriteLine("[docker] no image found - pulling latest");
                await this.Client.Images.CreateImageAsync(new ImagesCreateParameters { FromImage = EventStoreImage, Tag = "latest" }, null, IgnoreProgress.Forever);
            }
            Console.WriteLine("[docker] creating container " + EventStoreContainer);
            //Create container ...
            await this.Client.Containers.CreateContainerAsync(
                new CreateContainerParameters
                { 
                    Image = EventStoreImage, 
                    Name = EventStoreContainer, 
                    Tty = true,
                    HostConfig = new HostConfig
                    {
                        PortBindings = new Dictionary<string, IList<PortBinding>>
                        {
                            { 
                                "2113/tcp", 
                                new List<PortBinding> { 
                                    new PortBinding
                                    {
                                        HostPort = "2113"
                                    } 
                                }
                            },
                            { 
                                "1113/tcp", 
                                new List<PortBinding> { 
                                    new PortBinding
                                    {
                                        HostPort = "1113"
                                    } 
                                }
                            }
                        }
                    }
                });
            // Starting the container ...
            Console.WriteLine("[docker] starting container " + EventStoreContainer);
            await this.Client.Containers.StartContainerAsync(EventStoreContainer, new ContainerStartParameters { });
            var endpoint = new Uri("tcp://127.0.0.1:1113");
            var settings = ConnectionSettings
                .Create()
                .KeepReconnecting()
                .KeepRetrying()
                .SetDefaultUserCredentials(new UserCredentials("admin", "changeit"));
            var connectionName = $"M={Environment.MachineName},P={Process.GetCurrentProcess().Id},T={DateTimeOffset.UtcNow.Ticks}";
            this.Connection = EventStoreConnection.Create(settings, endpoint, connectionName);
            Console.WriteLine("[docker] connecting to eventstore");
            await this.Connection.ConnectAsync();
        }

        public async Task DisposeAsync()
        {
            if(this.Client != null)
            {
                this.Connection?.Dispose();
                Console.WriteLine("[docker] stopping container " + EventStoreContainer);                
                await this.Client.Containers.StopContainerAsync(EventStoreContainer, new ContainerStopParameters { });
                Console.WriteLine("[docker] removing container " + EventStoreContainer);                
                await this.Client.Containers.RemoveContainerAsync(EventStoreContainer, new ContainerRemoveParameters { Force = true });
                this.Client.Dispose();
            }
        }

        private DockerClient Client { get; set; }

        private class IgnoreProgress : IProgress<JSONMessage>
        {
            public static readonly IProgress<JSONMessage> Forever = new IgnoreProgress();

            public void Report(JSONMessage value) { }
        }
    }
#endif
}