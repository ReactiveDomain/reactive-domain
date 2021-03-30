using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using ReactiveDomain.Util;

namespace ReactiveDomain.Messaging.Bus
{

    /// <inheritdoc cref="IDispatcher"/>
    public class Dispatcher : IDispatcher
    {
        private static readonly ILogger Log = Logging.LogProvider.GetLogger("ReactiveDomain"); // might be unused... need to discuss

        private readonly Dictionary<Type, object> _handleWrappers;
        private readonly MultiQueuedPublisher _queuedPublisher;
        private readonly InMemoryBus _bus;
        private bool _disposed;
        public bool Idle => _queuedPublisher.Idle;
        public Dispatcher(
                    string name,
                    uint queueCount = 0,
                    bool watchSlowMsg = false,
                    TimeSpan? slowMsgThreshold = null,
                    TimeSpan? slowCmdThreshold = null)
        {
            var slowMsgThreshold1 = slowMsgThreshold ?? TimeSpan.FromMilliseconds(100);
            var slowCmdThreshold1 = slowCmdThreshold ?? TimeSpan.FromMilliseconds(500);
            _bus = new InMemoryBus(name, watchSlowMsg, slowMsgThreshold);
            _queuedPublisher = new MultiQueuedPublisher(_bus, queueCount, slowMsgThreshold1, slowCmdThreshold1);
            _handleWrappers = new Dictionary<Type, object>();
        }


        /// <summary>
        /// Enqueue a command and block until completed
        /// </summary>
        /// <param name="command"></param>
        /// <param name="exceptionMsg"></param>
        /// <param name="responseTimeout"></param>
        /// <param name="ackTimeout"></param>
        /// <returns></returns>
        public void Send(
                        ICommand command,
                        string exceptionMsg = null,
                        TimeSpan? responseTimeout = null,
                        TimeSpan? ackTimeout = null)
            => _queuedPublisher.Send(command, exceptionMsg, responseTimeout, ackTimeout);

        /// <summary>
        ///  Enqueue a command and block until completed
        /// </summary>
        /// <param name="command"></param>
        /// <param name="response"></param>
        /// <param name="responseTimeout"></param>
        /// <param name="ackTimeout"></param>
        /// <returns>Command returned success</returns>
        public bool TrySend(
                        ICommand command,
                        out CommandResponse response,
                        TimeSpan? responseTimeout = null,
                        TimeSpan? ackTimeout = null)
            => _queuedPublisher.TrySend(command, out response, responseTimeout, ackTimeout);

        /// <summary>
        /// Enqueue a command and return
        /// </summary>
        /// <param name="command"></param>
        /// <param name="responseTimeout"></param>
        /// <param name="ackTimeout"></param>
        /// <returns>Command enqueued</returns>
        public bool TrySendAsync(
                        ICommand command,
                        TimeSpan? responseTimeout = null,
                        TimeSpan? ackTimeout = null)
            => _queuedPublisher.TrySendAsync(command, responseTimeout, ackTimeout);

        public IDisposable Subscribe<T>(IHandleCommand<T> handler) where T : class, ICommand
        {
            if (HasSubscriberFor<T>())
                throw new ExistingHandlerException("Duplicate registration for command type.");
            var handleWrapper = new CommandHandler<T>(_bus, handler);
            _handleWrappers.Add(typeof(T), handleWrapper);
            Subscribe(handleWrapper, false);
            return new Disposer(() => { Unsubscribe(handler); return Unit.Default; });
        }
       
        public void Unsubscribe<T>(IHandleCommand<T> handler) where T : class, ICommand
        {
            if (!_handleWrappers.TryGetValue(typeof(T), out var wrapper)) return;
            Unsubscribe((CommandHandler<T>)wrapper);
            _handleWrappers.Remove(typeof(T));
        }

        public void Publish(IMessage message)
            => _queuedPublisher.Publish(message);

        public IDisposable Subscribe<T>(IHandle<T> handler, bool includeDerived = true) where T : class, IMessage
            => _bus.Subscribe(handler, includeDerived);
        public IDisposable SubscribeToAll(IHandle<IMessage> handler)
                   => _bus.SubscribeToAll(handler);

        public void Unsubscribe<T>(IHandle<T> handler) where T : class, IMessage
        {
            _bus.Unsubscribe(handler);
        }

        public bool HasSubscriberFor<T>(bool includeDerived = false) where T : class, IMessage
            => _bus.HasSubscriberFor<T>(includeDerived);

        public string Name => _bus.Name;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            _disposed = true;
            if (disposing)
            {
                _queuedPublisher?.Dispose();
            }
        }
    }
}
