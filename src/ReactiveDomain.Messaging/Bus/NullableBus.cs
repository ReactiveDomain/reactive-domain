using System;

namespace ReactiveDomain.Messaging.Bus
{
    /// <summary>
    /// A bus you can turn off.
    /// 
    /// Subscriptions and unsubscribe are always redirected to the target.
    /// 
    /// When RedirectToNull == true.
    /// Drops all command and Events published.
    /// Returns success for any TryFire.
    /// 
    /// </summary>
    public class NullableBus : IDispatcher
    {
        private IDispatcher _target;
        public bool Idle => _target.Idle;

        public NullableBus(IDispatcher target, bool directToNull = true, string name = null)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            Name = name ?? _target.Name;
            RedirectToNull = directToNull;
        }

        public bool RedirectToNull { get; set; }

        #region Implementation of ICommandPublisher

        public void Send(ICommand command, string exceptionMsg = null, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null)
        {
            if (RedirectToNull || _target == null) return;
            _target.Send(command, exceptionMsg, responseTimeout, ackTimeout);
        }

        public bool TrySend(ICommand command, out CommandResponse response, TimeSpan? responseTimeout = null,
            TimeSpan? ackTimeout = null)
        {
            if (RedirectToNull || _target == null)
            {
                response = command.Succeed();
                return true;
            }
            return _target.TrySend(command, out response, responseTimeout, ackTimeout);
        }

        public bool TrySendAsync(ICommand command, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null)
        {
            if (RedirectToNull) return true;
            return _target?.TrySendAsync(command, responseTimeout, ackTimeout) ?? false;
        }

        #endregion

        #region Implementation of ICommandSubscriber

        public IDisposable Subscribe<T>(IHandleCommand<T> handler) where T : class, ICommand
        {
            return _target?.Subscribe(handler);
        }
        
        public void Unsubscribe<T>(IHandleCommand<T> handler) where T : class, ICommand
        {
            _target?.Unsubscribe(handler);
        }

        #endregion

        #region Implementation of IPublisher

        public void Publish(IMessage message)
        {
            if (RedirectToNull) return;
            _target?.Publish(message);
        }

        #endregion

        #region Implementation of ISubscriber

        public IDisposable Subscribe<T>(IHandle<T> handler, bool includeDerived = true) where T : class,IMessage
        {

            return _target?.Subscribe(handler);
        }
        public IDisposable SubscribeToAll(IHandle<IMessage> handler)
        {
            return _target?.SubscribeToAll(handler);
        }

        public void Unsubscribe<T>(IHandle<T> handler) where T : class, IMessage
        {

            _target?.Unsubscribe(handler);
        }

        public bool HasSubscriberFor<T>(bool includeDerived = false) where T : class, IMessage
        {

            return _target?.HasSubscriberFor<T>(includeDerived) ?? false;
        }

        #endregion

        #region Implementation of IBus

        public string Name { get; }

        #endregion
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        private bool _disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                RedirectToNull = true;
                _target = null;
            }
            _disposed = true;
        }
    }
}
