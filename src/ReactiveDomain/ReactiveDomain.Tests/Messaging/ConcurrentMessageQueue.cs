using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ReactiveDomain.Messages;
using ReactiveDomain.Messaging;
using Xunit;

namespace ReactiveDomain.Tests.Messaging
{
    public class ConcurrentMessageQueue<T> : ConcurrentQueue<T> where T : Message
    {
        private readonly string _name;

        public ConcurrentMessageQueue(string name)
        {
            _name = name;
        }

        public ConcurrentMessageQueue(string name, IEnumerable<T> items)
            : base(items)
        {
            _name = name;
        }

        public TMsg DequeueNext<TMsg>() where TMsg : T
        {

            T outVal;
            if (IsEmpty)
                throw new Exception($" {_name} queue: Type {typeof(TMsg).Name} not found Queue is Empty");
            if (!TryDequeue(out outVal))
                throw new Exception($" {_name} queue: Unable to dequeue next item.");
            if (!(outVal is TMsg))
            {
                throw new Exception($" {_name} queue: Type <{typeof(TMsg).Name}> is not next item, instead <{outVal.GetType().Name}> found.");
            }
            return (TMsg)outVal;
        }
        public ConcurrentMessageQueue<T> AssertNext<TMsg>(Guid correlationId, out TMsg msg) where TMsg : T, ICorrelatedMessage
        {
            msg = DequeueNext<TMsg>();
            if (msg.CorrelationId != correlationId)
            {
                throw new Exception($" {_name} queue: Message type <{typeof(TMsg).Name}> found with incorrect corelationId. Expected [{correlationId}] found [{msg.CorrelationId}] instead.");
            }
            return this;
        }
        public ConcurrentMessageQueue<T> AssertNext<TMsg>(Guid correlationId) where TMsg : T, ICorrelatedMessage
        {
            TMsg ignore;
            AssertNext<TMsg>(correlationId, out ignore);
            return this;
        }
        public ConcurrentMessageQueue<T> AssertNext<TMsg>(
                        Func<TMsg, bool> condition, 
                        string userMessage = null) where TMsg : T, ICorrelatedMessage
        {
            TMsg msg = DequeueNext<TMsg>();
            Assert.True(condition(msg), userMessage);
            return this;
        }
        public void AssertEmpty()
        {
            if (!IsEmpty)
                throw new Exception($" {_name} Queue not Empty.");
        }

        // JUST INHERITING EVERYTHING
    }
}
