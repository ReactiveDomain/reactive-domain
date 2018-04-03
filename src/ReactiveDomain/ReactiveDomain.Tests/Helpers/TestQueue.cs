using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ReactiveDomain.Bus;
using ReactiveDomain.Messaging;
using ReactiveDomain.Tests.Messaging;

namespace ReactiveDomain.Tests.Helpers
{
    public class TestQueue : IHandle<Message>
    {

        public ConcurrentMessageQueue<Message> Messages { get; }
        private readonly HashSet<Message> _messageSet;
        public ConcurrentMessageQueue<Command> Commands { get; }
        public ConcurrentMessageQueue<CommandResponse> Responses { get; }
        public ConcurrentMessageQueue<DomainEvent> Events { get; }
        private ConcurrentMessageQueue<Message> _waitQueue { get; }

        public TestQueue(ISubscriber bus = null)
        {
            Messages = new ConcurrentMessageQueue<Message>("Messages");
            _messageSet = new HashSet<Message>();
            Commands = new ConcurrentMessageQueue<Command>("Commands");
            Responses = new ConcurrentMessageQueue<CommandResponse>("Responses");
            Events = new ConcurrentMessageQueue<DomainEvent>("Events");
            _waitQueue = new ConcurrentMessageQueue<Message>("Wait Queue");

            bus?.Subscribe(this);
        }
        public void Handle(Message message)
        {
            while (Interlocked.Read(ref _cleaning) != 0)
                Thread.Sleep(10);

            Messages.Enqueue(message);
            lock (_messageSet)
                _messageSet.Add(message);
            if (message is DomainEvent)
                Events.Enqueue(message as DomainEvent);
            else if (message is Command)
                Commands.Enqueue(message as Command);
            else if (message is CommandResponse)
                Responses.Enqueue(message as CommandResponse);
            if (Interlocked.Read(ref _waiting) == 1)
                _waitQueue.Enqueue(message);
        }

        private long _cleaning = 0;
        public void Clear()
        {
            try
            {
                Interlocked.Exchange(ref _cleaning, 1); //It's ok to clean an extra message on the race condition
                Message msg;
                while (!Messages.IsEmpty)
                    Messages.TryDequeue(out msg);
                while (!_waitQueue.IsEmpty)
                    _waitQueue.TryDequeue(out msg);
                Command cmd;
                while (!Commands.IsEmpty)
                    Commands.TryDequeue(out cmd);
                DomainEvent evt;
                while (!Events.IsEmpty)
                    Events.TryDequeue(out evt);
                CommandResponse resp;
                while (!Responses.IsEmpty)
                    Responses.TryDequeue(out resp);
                lock (_messageSet)
                    _messageSet.Clear();
            }
            finally
            {
                Interlocked.Exchange(ref _cleaning, 0);
            }

        }

        private long _waiting = 0;

        public void WaitFor<T>(TimeSpan timeout) where T : Message
        {
            try
            {
                lock (_messageSet)
                {
                    if (_messageSet.Any(m => m is T))
                        return;
                }

                Interlocked.Exchange(ref _waiting, 1);
                var deadline = DateTime.Now + timeout;

                do
                {
                    while (!_waitQueue.IsEmpty)
                    {
                        Message msg;
                        _waitQueue.TryDequeue(out msg);
                        if (msg is T) return;
                    }
                    if (DateTime.Now > deadline)
                        throw new TimeoutException();
                    Thread.Sleep(50);
                } while (true);

            }
            finally
            {
                Interlocked.Exchange(ref _waiting, 0);
                Message msg;
                while (!_waitQueue.IsEmpty)
                    _waitQueue.TryDequeue(out msg);
            }
        }

    }
    public static class TestQueueExtentions
    {
        //  public static TestQueue StartsWith<T>(this TestQueue queue, Type MsageType, )
    }


}
