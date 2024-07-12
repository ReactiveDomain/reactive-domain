using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Testing
{
    /// <summary>
    /// Has a ConcurrentMessageQueue that subscribes to all messages of the specified types on the specified ISubscriber.
    /// </summary>
    public sealed class TestQueue : IHandle<IMessage>, IDisposable
    {
        /// <summary>
        /// The types of messages to subscribe to. Child types are included.
        /// </summary>
        public Type[] MessageTypeFilter => (Type[])_msgTypeFilter.Clone();
        private readonly Type[] _msgTypeFilter;
        private readonly bool _isFiltered;
        /// <summary>
        /// Contains the list of the Types of messages procesed since last time TestQueue was cleared.
        /// If Type tracking is disabled, returns an empty list.
        /// </summary>
        public Type[] HandledTypes => _handledTypes.ToArray();
        private readonly HashSet<Type> _handledTypes = new HashSet<Type>();
        private readonly bool _trackTypes;
        private readonly Dictionary<Guid, ManualResetEventSlim> _idWatchList = new Dictionary<Guid, ManualResetEventSlim>();
        /// <summary>
        /// The queue of messages.
        /// </summary>
        public ConcurrentMessageQueue<IMessage> Messages { get; }

        private long _cleaning = 0;
        private long _queueVersion = 0; //queue version is incremented on each clean
        private readonly IDisposable _subscription;

        /// <summary>
        /// Create a test queue on the specified ISubscriber.
        /// </summary>
        /// <param name="subscriber">Where to subscribe.</param>
        /// <param name="messageTypeFilter">List of message types to accumulate in the queue.</param>
        /// <param name="trackTypes">Whether to enable message-type-based functions such as WaitFor.</param>
        public TestQueue(
                    ISubscriber subscriber = null,
                    Type[] messageTypeFilter = null,
                    bool trackTypes = true)
        {
            _isFiltered = messageTypeFilter != null;
            _msgTypeFilter = messageTypeFilter ?? new Type[] { };

            _trackTypes = trackTypes;

            Messages = new ConcurrentMessageQueue<IMessage>("Messages");

            _subscription = subscriber?.SubscribeToAll(this);
        }
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) { return; }
            _disposed = true;
            _subscription?.Dispose();
            Clear();
        }
        private bool EnsureReady()
        {
            if (_disposed) { throw new ObjectDisposedException(nameof(TestQueue)); }
            if (Interlocked.Read(ref _cleaning) != 0)
            {
                SpinWait.SpinUntil(() => Interlocked.Read(ref _cleaning) == 0, 250);
            }
            if (Interlocked.Read(ref _cleaning) != 0)
            {
                throw new TimeoutException("Test Queue not ready, timeout clearing queues");
            }
            return true;
        }
        public void Handle(IMessage message)
        {
            EnsureReady();
            var msgType = message.GetType();
            if (_isFiltered && !MessageTypeFilter.Any(t => t.IsAssignableFrom(msgType))) { return; }

            Messages.Enqueue(message);

            if (_trackTypes) { _handledTypes.Add(msgType); }
            lock (_idWatchList)
            {
                if (_idWatchList.ContainsKey(message.MsgId))
                {
                    _idWatchList[message.MsgId].Set();
                }
            }
        }

        /// <summary>
        /// Clear the queue of messages.
        /// </summary>
        public void Clear()
        {
            try
            {
                Interlocked.Increment(ref _queueVersion); //invalidate current version
                Interlocked.Exchange(ref _cleaning, 1); //It's ok to clean an extra message on the race condition
                while (!Messages.IsEmpty)
                    Messages.TryDequeue(out var _);
                _handledTypes.Clear();
                lock (_idWatchList)
                {
                    _idWatchList.Clear();
                }
            }
            finally
            {
                Interlocked.Exchange(ref _cleaning, 0);
            }
        }

        /// <summary>
        /// Wait for the first message of type T to appear in the queue.
        /// </summary>
        /// <typeparam name="T">The type to wait for.</typeparam>
        /// <param name="timeout">How long to wait before timing out.</param>
        public void WaitFor<T>(TimeSpan timeout) where T : IMessage
        {
            WaitForMultiple<T>(1, timeout);
        }

        /// <summary>
        /// Wait for some number of messages compatible with T (via the 'is' operator) to appear in the queue.
        /// </summary>
        /// <typeparam name="T">The type to wait for.</typeparam>
        /// <param name="num">The number of messages of the given type to wait for.</param>
        /// <param name="timeout">How long to wait before timing out.</param>
        public void WaitForMultiple<T>(uint num, TimeSpan timeout) where T : IMessage
        {
            EnsureReady();
            var version = Interlocked.Read(ref _queueVersion);
            if (!_trackTypes) { throw new InvalidOperationException("Type tracking is disabled for this instance."); }

            var startTime = Environment.TickCount; //returns MS since machine start
            var endTime = startTime + (int)timeout.TotalMilliseconds;

            var delay = 1;
            //Evaluating the entire queue is a bit heavy, but is required to support waiting on base types, interfaces, etc. 
            //calling ToList allows the concurrent queue to handle grabbing a snapshot of the collection
            while (Messages.ToList().Count(x => x is T) < num)
            {
                if (_disposed) { throw new ObjectDisposedException(nameof(TestQueue)); }
                if (Interlocked.Read(ref _queueVersion) != version) { throw new InvalidOperationException("Test queue Cleared!"); }
                var now = Environment.TickCount;
                if ((endTime - now) <= 0) { throw new TimeoutException(); }

                if (delay < 250) { delay = delay << 1; }
                delay = Math.Min(delay, endTime - now);
                Thread.Sleep(delay);
            }
        }
        /// <summary>
        /// Wait for a message with a specific MsgId to appear in the queue.
        /// </summary>
        /// <param name="id">The MsgId to wait for.</param>
        /// <param name="timeout">How long to wait before timing out.</param>
        public void WaitForMsgId(Guid id, TimeSpan timeout)
        {
            EnsureReady();
            var version = Interlocked.Read(ref _queueVersion);
            var deadline = DateTime.Now + timeout;

            //setup a watch
            ManualResetEventSlim waithandle;
            lock (_idWatchList)
            {
                if (_idWatchList.ContainsKey(id))
                {
                    waithandle = _idWatchList[id];
                    if (waithandle.IsSet) { return; }
                }
                else
                {
                    waithandle = new ManualResetEventSlim(false);
                    _idWatchList.Add(id, waithandle);
                }
            }
            //check to see if the message was handled before we got the watch setup
            if (Messages.ToArray().Any(m => m.MsgId == id))
            {
                lock (_idWatchList)
                {
                    _idWatchList[id].Set();                       
                }
                return;
            }
            //wait here to see if the message handler triggers the wait handle we added
            while (!waithandle.Wait(10))
            {
                if (DateTime.Now > deadline) { throw new TimeoutException($"Msg with ID {id} failed to arrive within {timeout}."); }
                if (_disposed) { throw new ObjectDisposedException(nameof(TestQueue)); }
                if (Interlocked.Read(ref _queueVersion) != version) { throw new InvalidOperationException("Test queue Cleared!"); }
            }

        }

        #region Passthroughs to Message Queue

        /// <summary>
        /// Dequeue the next message on the Messages queue if it is of type TMsg.
        /// </summary>
        /// <typeparam name="TMsg">The expected type of the next message in the queue.</typeparam>
        /// <returns>The message that was dequeued.</returns>
        public TMsg DequeueNext<TMsg>() where TMsg : IMessage
        {
            EnsureReady();
            return Messages.DequeueNext<TMsg>();
        }

        /// <summary>
        /// Assert that the next message on the Messages queue is of type TMsg and has the specified correlation ID.
        /// </summary>
        /// <typeparam name="TMsg">The expected type of the next message in the queue.</typeparam>
        /// <param name="correlationId">The correlation ID that the message must have.</param>
        /// <param name="msg">The message that was dequeued.</param>
        /// <returns>The Messages queue after dequeueing the next message.</returns>
        public ConcurrentMessageQueue<IMessage> AssertNext<TMsg>(
                    Guid correlationId,
                    out TMsg msg) where TMsg : ICorrelatedMessage
        {
            EnsureReady();
            return Messages.AssertNext<TMsg>(correlationId, out msg);
        }

        /// <summary>
        /// Assert that the next message on the Messages queue is of type TMsg.
        /// </summary>
        /// <typeparam name="TMsg">The expected type of the next message in the queue.</typeparam>
        /// <param name="correlationId">The correlation ID that the message must have.</param>
        /// <returns>The Messages queue after dequeueing the next message.</returns>
        public ConcurrentMessageQueue<IMessage> AssertNext<TMsg>(Guid correlationId) where TMsg : ICorrelatedMessage
        {
            EnsureReady();
            return Messages.AssertNext<TMsg>(correlationId);
        }

        /// <summary>
        /// Assert that the next message on the Messages queue is of type TMsg.
        /// </summary>
        /// <typeparam name="TMsg">The expected type of the next message in the queue.</typeparam>
        /// <param name="condition">The condition that must be satisfied by the message.</param>
        /// <param name="userMessage">A message to print in the exception if the condition is not met or if the type is wrong.</param>
        /// <returns>The Messages queue after dequeueing the next message.</returns>
        public ConcurrentMessageQueue<IMessage> AssertNext<TMsg>(
                    Func<TMsg, bool> condition,
                    string userMessage = null) where TMsg : ICorrelatedMessage
        {
            EnsureReady();
            return Messages.AssertNext<TMsg>(condition, userMessage);
        }

        /// <summary>
        /// Assert that the Messages queue is empty.
        /// </summary>
        public void AssertEmpty()
        {
            EnsureReady();
            Messages.AssertEmpty();
        }

        #endregion
    }
}
