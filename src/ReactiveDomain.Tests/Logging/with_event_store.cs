using System;
using System.IO;
using System.Net;
using EventStore.ClientAPI;
using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Legacy.CommonDomain;
using ReactiveDomain.Messaging;
using ReactiveDomain.Tests.Specifications;
using BootStrap = ReactiveDomain.Foundation.BootStrap;

namespace ReactiveDomain.Tests.Logging
{
    // ReSharper disable once InconsistentNaming
    public abstract class with_event_store :
        CommandBusSpecification,
        IDisposable
    {
        protected EventStoreLoader EventStore;
        protected IEventStoreConnection TcpEndPointConnection;
        protected IRepository Repo;

        private static readonly IPEndPoint IntegrationTestTcpEndPoint;

        static with_event_store()
        {
            BootStrap.Load();
            IntegrationTestTcpEndPoint = new IPEndPoint(IPAddress.Loopback, 1113);
        }

        protected override void Given()
        {
            TcpEndPointConnection = EventStoreConnection.Create(IntegrationTestTcpEndPoint);
            EventStore = new EventStoreLoader();

            TcpEndPointConnection.ConnectAsync().Wait();

            //TODO: better path!  Tacky because PerkinElmer\Greylock...
            EventStore.SetupEventStore(
                new DirectoryInfo(
                    @"c:\program files\PerkinElmer\Greylock\eventStore"));

            Repo = new GetEventStoreRepository(EventStore.Connection);
        }

        #region IDisposable
        public void Dispose()
        {
            Dispose(true);
        }

        private bool _disposed;

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                TcpEndPointConnection.Close();
            }
            _disposed = true;
        }

        #endregion
    }
}
