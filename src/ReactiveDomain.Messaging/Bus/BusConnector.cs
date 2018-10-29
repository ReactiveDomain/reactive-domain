using System;
using System.Collections.Generic;
using ReactiveDomain.Util;

namespace ReactiveDomain.Messaging.Bus {
    /// <summary>
    /// This class allows connecting two buses to share message traffic without echoing
    /// </summary>
    public class BusConnector : IDisposable {
        private readonly IDispatcher _left;
        private readonly IDispatcher _right;
        private readonly HashSet<Guid> _fromLeft = new HashSet<Guid>();
        private readonly HashSet<Guid> _fromRight = new HashSet<Guid>();
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        public BusConnector(IDispatcher left, IDispatcher right) {
            _left = left;
            _right = right;
            _subscriptions.Add(_left.Subscribe(new AdHocHandler<Message>(FromLeft)));
            _subscriptions.Add(_right.Subscribe(new AdHocHandler<Message>(FromRight)));

        }

        private void FromLeft(Message msg) {
            lock (_fromRight) {
                if (_fromRight.Contains(msg.MsgId)) {
                    _fromRight.Remove(msg.MsgId);
                    return;
                }
            }
            lock (_fromLeft) {
                _fromLeft.Add(msg.MsgId);
            }
            if (msg is Command command) {
                _right.TrySendAsync(command);
            }
            else
                _right.Publish(msg);

        }
        private void FromRight(Message msg) {
            lock (_fromLeft) {
                if (_fromLeft.Contains(msg.MsgId)) {
                    _fromLeft.Remove(msg.MsgId);
                    return;
                }
            }
            lock (_fromRight) {
                _fromRight.Add(msg.MsgId);
            }
            if (msg is Command command) {
                _left.TrySendAsync(command);
            }
            else
                _left.Publish(msg);

        }
        public void Dispose() {
            foreach (var subscription in _subscriptions) {
                subscription?.Dispose();
            }
        }
    }
}
