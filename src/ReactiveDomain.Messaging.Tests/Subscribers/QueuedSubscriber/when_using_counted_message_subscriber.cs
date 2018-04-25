using System;
using System.Threading;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber {
    // ReSharper disable InconsistentNaming
    public abstract class when_using_counted_message_subscriber :
                            IHandle<Message>,
                            IHandle<CountedEvent>,
                            IHandle<CountedTestMessage>,
                            IDisposable {
       
        protected IDispatcher Bus;
        private long _msgCount;
        private long _eventCount;
        private long _testMsgCount;
        private long _lastMessageCounter;
        private bool _isInOrder;
        public long MsgCount => _msgCount;
        public long EventCount => _eventCount;
        public long TestMsgCount => _testMsgCount;
        public bool IsInOrder => _isInOrder;

        public void Clear() {
           Interlocked.Exchange(ref _msgCount , 0);
            Interlocked.Exchange(ref _eventCount , 0);
            Interlocked.Exchange(ref _testMsgCount , 0);
            Interlocked.Exchange(ref _lastMessageCounter , -1);
            _isInOrder = true;
        }

        protected when_using_counted_message_subscriber() {
            Monitor.Enter(QueuedSubscriberLock.LockObject);
            Bus = new Dispatcher(nameof(when_using_queued_subscriber));
            Bus.Subscribe<Message>(this);
            Bus.Subscribe<CountedEvent>(this);
            Bus.Subscribe<CountedTestMessage>(this);
        }
        public void Handle(Message message) {
            Interlocked.Increment(ref _msgCount);
        }
        public void Handle(CountedEvent message) {
            if (message.MessageNumber != _lastMessageCounter + 1)
                _isInOrder = false;
            Interlocked.Increment(ref _lastMessageCounter);
            Interlocked.Increment(ref _eventCount);
        }
        public void Handle(CountedTestMessage message) {
            Interlocked.Increment(ref _testMsgCount);
        }
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposing) return;
            Monitor.Exit(QueuedSubscriberLock.LockObject);
            Bus?.Dispose();
        }
    }
}
