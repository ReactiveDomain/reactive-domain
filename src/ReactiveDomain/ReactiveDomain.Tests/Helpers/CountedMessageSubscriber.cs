﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveDomain.Bus;

namespace ReactiveDomain.Tests.Helpers
{
    /// <summary>
    /// Class used for testing QueuedSubscriber 
    /// - handles messages and events that contain a count (sequence number)
    /// that can be used to check message ordering.
    /// </summary>
    public class CountedMessageSubscriber :
                    QueuedSubscriber,
                    IHandle<CountedTestMessage>,
                    IHandle<CountedEvent>
    {
        public long MessagesHandled;
        public long LastMessageHandled;

        public long EventsHandled;
        public long LastEventHandled;

        public List<int> MsgOrder;
        public  List<int> EventOrder;
        public CountedMessageSubscriber(IGeneralBus bus) : base(bus)
        {
            EventOrder = new List<int>();
            MsgOrder = new List<int>();
            Subscribe<CountedTestMessage>(this);
            Subscribe<CountedEvent>(this);
        }

        public override void HandleDynamic(dynamic message)
        {
            Handle(message);
        }

        public void Handle(CountedTestMessage message)
        {
            MessagesHandled++;
            LastMessageHandled = message.MessageNumber;

            MsgOrder.Add(message.MessageNumber);            

        }

        public void Handle(CountedEvent message)
        {
            EventsHandled++;
            LastEventHandled = message.MessageNumber;
            EventOrder.Add(message.MessageNumber);

        }

        /// <summary>
        /// Verify that messages have been handled in the order they were published
        /// </summary>
        /// <returns></returns>
        public bool MessagesInOrder()
        {
            int lastMsg = 0;
            foreach (int i in MsgOrder)
            {
                if (lastMsg > i) return false;
                lastMsg = i;
            }
            return true;
        }

        /// <summary>
        /// Verify that events have been handled in the order they were published
        /// </summary>
        /// <returns></returns>
        public bool EventsInOrder()
        {
            int lastMsg = -1;
            foreach (int i in EventOrder)
            {
                if (lastMsg > i)
                    return false;
                if (lastMsg != i-1)
                    return false;
                lastMsg = i;
            }
            return true;
        }
    }
}
