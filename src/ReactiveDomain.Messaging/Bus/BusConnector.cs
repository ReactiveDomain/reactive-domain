using System;
using System.Collections.Generic;
using ReactiveDomain.Util;

namespace ReactiveDomain.Messaging.Bus
{
    /// <summary>
    /// This class allows connecting two buses to share message traffic without echoing
    /// </summary>
    public class BusConnector : IDisposable
    {
        private readonly BusAdapter _left;
        private readonly BusAdapter _right;
        public BusConnector(IDispatcher left, IDispatcher right)
        {
            _left = new BusAdapter(left);
            _right = new BusAdapter(right);

            _left.Subscribe(_right);
            _right.Subscribe(_left);
        }

        public void Dispose()
        {
            _right.Unsubscribe(_left);
            _left.Unsubscribe(_right);
        }
    }

    public class BusAdapter :
        IHandle<Message>,
        ISubscriber
    {
        private readonly IDispatcher _bus;
        private MessageIdTracker _idTracker;
        private object _handler;
        private readonly HashSet<Guid> _trackedMessages;

        public BusAdapter(IDispatcher bus)
        {
            _idTracker = null;
            _bus = bus;
            _trackedMessages = new HashSet<Guid>();
        }

        public void Handle(Message message)
        {
            if (_trackedMessages.Remove(message.MsgId))
                return;
            if (message is Command)
            {
                _bus.TrySendAsync((Command) message);
            }
            else
                _bus.Publish(message);
        }

        public IDisposable Subscribe<T>(IHandle<T> handler) where T : Message
        {
            if (_idTracker != null) throw new ArgumentException("Cannot subscribe more than one tracked handler");
            if (typeof(T) != typeof(Message)) throw new ArgumentException("Only Message type subscriptions supported");
            _handler = handler;
            _idTracker = new MessageIdTracker((IHandle<Message>)handler, (g) => _trackedMessages.Add(g));
            // ReSharper disable once RedundantTypeArgumentsOfMethod
            _bus.Subscribe<Message>(_idTracker);
            // ReSharper disable once ConstantConditionalAccessQualifier
            return new Disposer(() => { this?.Unsubscribe(handler); return Unit.Default; });
        }

        public void Unsubscribe<T>(IHandle<T> handler) where T : Message
        {
            if (_handler == null || _idTracker == null) return;
            if (!ReferenceEquals(handler, (IHandle<T>)_handler))
                throw new ArgumentException("Handler is not current registered handler");
            // ReSharper disable once RedundantTypeArgumentsOfMethod
            _bus.Unsubscribe<Message>(_idTracker);
            _handler = null;
            _idTracker = null;
        }

        public bool HasSubscriberFor<T>(bool includeDerived = false) where T : Message
        {
            return _bus.HasSubscriberFor<T>(includeDerived);
        }
    }

    public class MessageIdTracker :
        IHandle<Message>
    {
        private readonly IHandle<Message> _target;
        private readonly Action<Guid> _tracker;

        public MessageIdTracker(IHandle<Message> target, Action<Guid> tracker)
        {
            _target = target;
            _tracker = tracker;
        }

        public void Handle(Message message)
        {
            _tracker(message.MsgId);
            _target.Handle(message);
        }
    }
}
