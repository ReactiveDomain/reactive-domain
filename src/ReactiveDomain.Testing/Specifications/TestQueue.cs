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
        public Type[] HandledTypes { get { return _handledTypes.ToArray(); } }
        public readonly HashSet<Type> _handledTypes = new HashSet<Type>();
        private readonly bool _trackTypes;
        private readonly ConcurrentDictionary<Guid, ManualResetEventSlim> _idWatchList = new ConcurrentDictionary<Guid, ManualResetEventSlim>();

        /// <summary>
        /// The queue of messages.
        /// </summary>
        public ConcurrentMessageQueue<IMessage> Messages { get; }

        private long _cleaning = 0;
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

        public void Handle(IMessage message)
        {
            if (_disposed) { return; }
            var msgType = message.GetType();
            if (_isFiltered && !MessageTypeFilter.Any(t => t.IsAssignableFrom(msgType))) { return; }

            SpinWait.SpinUntil(() => Interlocked.Read(ref _cleaning) == 0, 100);

            Messages.Enqueue(message);

            if (_trackTypes) { _handledTypes.Add(msgType); }
            if (_idWatchList.Count > 0)
            {
                _idWatchList.TryGetValue(message.MsgId, out var mres);
                mres?.Set();
            }
        }

        /// <summary>
        /// Clear the queue of messages.
        /// </summary>
        public void Clear()
        {
            try
            {
                Interlocked.Exchange(ref _cleaning, 1); //It's ok to clean an extra message on the race condition
                while (!Messages.IsEmpty)
                    Messages.TryDequeue(out var _);
                _handledTypes.Clear();
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
            if (_disposed) { throw new ObjectDisposedException(nameof(TestQueue)); }
            if (!_trackTypes) { throw new InvalidOperationException("Type tracking is disabled for this instance."); }
            var deadline = DateTime.Now + timeout;
            var msgType = typeof(T);
            do
            {
                if (SpinWait.SpinUntil(() => HandledTypes.Any(t => msgType.IsAssignableFrom(t)), 50))
                {
                    return;
                }
                if (DateTime.Now > deadline)
                {
                    throw new TimeoutException();
                }
                if (_disposed) { throw new ObjectDisposedException(nameof(TestQueue)); }

            } while (true);
        }

        /// <summary>
        /// Wait for a message with a specific MsgId to appear in the queue.
        /// </summary>
        /// <param name="id">The MsgId to wait for.</param>
        /// <param name="timeout">How long to wait before timing out.</param>
        public void WaitForMsgId(Guid id, TimeSpan timeout)
        {
            if (_disposed) { throw new ObjectDisposedException(nameof(TestQueue)); }
            var deadline = DateTime.Now + timeout;
            try
            {

                var mres = new ManualResetEventSlim();
                using (mres)
                {
                    var waithandle = mres;


                    if (!_idWatchList.TryAdd(id, waithandle))
                    {
                        if (!_idWatchList.TryGetValue(id, out waithandle) || waithandle.IsSet)
                        {
                            return; //we hit the race condition or waithandle set both likely mean it was found #mocklife
                        }
                    }

                    if (Messages.ToArray().Any(m => m.MsgId == id)) { waithandle.Set(); }

                    while (!waithandle.Wait(10))
                    {
                        if (DateTime.Now > deadline) { throw new TimeoutException(); }
                        if (_disposed) { throw new ObjectDisposedException(nameof(TestQueue)); }
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                //waithandle or TestQueue disposed
                return;
            }
            finally
            {
                _idWatchList.TryRemove(id, out _);
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
            return Messages.AssertNext<TMsg>(condition, userMessage);
        }

        /// <summary>
        /// Assert that the Messages queue is empty.
        /// </summary>
        public void AssertEmpty()
        {
            Messages.AssertEmpty();
        }

        #endregion
    }
}
