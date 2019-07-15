using System;
using System.Threading;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Messaging.Tests {
    public class RemoteBusFixture : IDisposable {
        public Dispatcher LocalBus { get; }
        public Dispatcher RemoteBus { get; }
        public long LocalMsgCount;
        public long RemoteMsgCount;
        public readonly TimeSpan StandardTimeout;
        private readonly BusConnector _connector;

        public RemoteBusFixture() {
            StandardTimeout = TimeSpan.FromMilliseconds(200);
            LocalBus = new Dispatcher(nameof(TestCommandBusFixture), 1, false, StandardTimeout, StandardTimeout);
            RemoteBus = new Dispatcher(nameof(TestCommandBusFixture), 1, false, StandardTimeout, StandardTimeout);

            LocalBus.SubscribeToAll(new AdHocHandler<IMessage>(_ => Interlocked.Increment(ref LocalMsgCount)));
            RemoteBus.SubscribeToAll(new AdHocHandler<IMessage>(_ => Interlocked.Increment(ref RemoteMsgCount)));
            
            _connector = new BusConnector(LocalBus, RemoteBus);
            
            Reset();
        }
        public void Reset() {
            Interlocked.Exchange(ref LocalMsgCount, 0);
            Interlocked.Exchange(ref RemoteMsgCount, 0);
        }
        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                LocalBus?.Dispose();
                RemoteBus?.Dispose();
                _connector?.Dispose();
            }
        }
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}