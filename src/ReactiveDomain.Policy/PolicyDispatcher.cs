using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Util;
using System;

namespace ReactiveDomain.Policy
{
    public class PolicyDispatcher : IDispatcher
    {
        private readonly IDispatcher _dispatcher;
        private readonly Func<UserPolicy> _getPolicy;
        public bool Idle => _dispatcher.Idle;
        public string Name => _dispatcher.Name;

        public PolicyDispatcher(IDispatcher dispatcher, Func<UserPolicy> getPolicy)
        {
            Ensure.NotNull(dispatcher, nameof(dispatcher));
            Ensure.NotNull(getPolicy, nameof(getPolicy));
            _dispatcher = dispatcher;
            _getPolicy = getPolicy;
        }
        public void Send(ICommand command, string exceptionMsg = null, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null)
        {
            if (_getPolicy().HasPermission(command.GetType()))
            {
                _dispatcher.Send(command, exceptionMsg, responseTimeout, ackTimeout);
            }
            else
            {
                var fail = (Fail)command.Fail(new AuthorizationException(command.GetType(), exceptionMsg));
                throw new CommandException(exceptionMsg ?? fail.Exception.Message, fail.Exception, command);
            }
        }
        public bool TrySend(ICommand command, out CommandResponse response, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null)
        {
            if (_getPolicy().HasPermission(command.GetType()))
            {
                return _dispatcher.TrySend(command, out response, responseTimeout, ackTimeout);
            }
            else
            {
                response = command.Fail(new AuthorizationException(command.GetType(), null));
                return false;
            }
        }
        public bool TrySendAsync(ICommand command, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null)
        {
            if (_getPolicy().HasPermission(command.GetType()))
            {
                return _dispatcher.TrySendAsync(command, responseTimeout, ackTimeout);
            }
            else
            {
                return false;
            }
        }
        public bool TrySendAsync(ICommand command, out AuthorizationException exception, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null)
        {
            if (_getPolicy().HasPermission(command.GetType()))
            {
                exception = null;
                return _dispatcher.TrySendAsync(command, responseTimeout, ackTimeout);
            }
            else
            {
                exception = new AuthorizationException(command.GetType(), null);
                return false;
            }
        }
        //delegated implementation
        public void Publish(IMessage message)
        {
            _dispatcher.Publish(message);
        }
        public IDisposable SubscribeToAll(IHandle<IMessage> handler)
        {
            return _dispatcher.SubscribeToAll(handler);
        }

        bool ISubscriber.HasSubscriberFor<T>(bool includeDerived)
        {
            return _dispatcher.HasSubscriberFor<T>(includeDerived);
        }
        IDisposable ICommandSubscriber.Subscribe<T>(IHandleCommand<T> handler)
        {
            return _dispatcher.Subscribe(handler);
        }
        IDisposable ISubscriber.Subscribe<T>(IHandle<T> handler, bool includeDerived)
        {
            return _dispatcher.Subscribe(handler, includeDerived);
        }
        void ICommandSubscriber.Unsubscribe<T>(IHandleCommand<T> handler)
        {
            _dispatcher.Unsubscribe(handler);
        }
        void ISubscriber.Unsubscribe<T>(IHandle<T> handler)
        {
            _dispatcher.Unsubscribe(handler);
        }
        public void Dispose()
        {
            _dispatcher.Dispose();
        }
    }
}
