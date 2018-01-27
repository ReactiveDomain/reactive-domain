using System;
using System.Collections.Generic;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Tests.Helpers;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber
{
    /// <summary>
    /// Class used for testing QueuedSubscriber 
    /// - handles messages and events that contain a count (sequence number)
    /// that can be used to check message ordering.
    /// </summary>
    public class CountedMessageSubscriber :
                    Messaging.Bus.QueuedSubscriber,
                    IHandle<CountedTestMessage>,
                    IHandle<CountedEvent>
    {
        public long MessagesHandled;
        public long LastMessageHandled;

        public long EventsHandled;
        public long LastEventHandled;

        public List<int> MsgOrder;
        public  List<int> EventOrder;
        private readonly Object _lockThis = new Object();

        public CountedMessageSubscriber(IGeneralBus bus) : base(bus)
        {
            EventOrder = new List<int>();
            MsgOrder = new List<int>();
            Subscribe<CountedTestMessage>(this);
            Subscribe<CountedEvent>(this);
        }

        public void Handle(CountedTestMessage message)
        {
            lock (_lockThis)
            {
                MessagesHandled++;
                LastMessageHandled = message.MessageNumber;

                MsgOrder.Add(message.MessageNumber);
            }

        }

        public void Handle(CountedEvent message)
        {
            lock (_lockThis)
            {
                EventsHandled++;
                LastEventHandled = message.MessageNumber;
                EventOrder.Add(message.MessageNumber);
            }

        }

        /// <summary>
        /// Verify that messages have been handled in the order they were published
        /// </summary>
        /// <returns></returns>
        public bool MessagesInOrder()
        {
            lock (_lockThis)
            {
                int lastMsg = 0;
                foreach (int i in MsgOrder)
                {
                    if (lastMsg > i) return false;
                    lastMsg = i;
                }
            }
            return true;
        }

        /// <summary>
        /// Verify that events have been handled in the order they were published
        /// </summary>
        /// <returns></returns>
        public bool EventsInOrder()
        {
            lock (_lockThis)
            {
                int lastMsg = -1;
                foreach (int i in EventOrder)
                {
                    if (lastMsg > i)
                        return false;
                    if (lastMsg != i - 1)
                        return false;
                    lastMsg = i;
                }
            }
            return true;
        }
    }
}
