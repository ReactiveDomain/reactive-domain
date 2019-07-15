using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ReactiveDomain.Messaging;
using Xunit;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Testing
{
    public class ConcurrentMessageQueue<T> : ConcurrentQueue<T> where T : IMessage
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
            if (IsEmpty)
                throw new Exception($" {_name} queue: Type {typeof(TMsg).Name} not found Queue is Empty");
            if (!TryDequeue(out var outVal))
                throw new Exception($" {_name} queue: Unable to dequeue next item.");
            if (!(outVal is TMsg))
                throw new Exception($" {_name} queue: Type <{typeof(TMsg).Name}> is not next item, instead <{outVal.GetType().Name}> found.");
            return (TMsg)outVal;
        }
        public ConcurrentMessageQueue<T> AssertNext<TMsg>(Guid correlationId, out TMsg msg) where TMsg :  ICorrelatedMessage, T
        {
            msg = DequeueNext<TMsg>();
            if (msg.CorrelationId != correlationId)
            {
                throw new Exception($" {_name} queue: Message type <{typeof(TMsg).Name}> found with incorrect correlationId. Expected [{correlationId}] found [{msg.CorrelationId}] instead.");
            }
            return this;
        }
        public ConcurrentMessageQueue<T> AssertNext<TMsg>(Guid correlationId) where TMsg :  ICorrelatedMessage, T
        {
            AssertNext<TMsg>(correlationId, out var _);
            return this;
        }
        public ConcurrentMessageQueue<T> AssertNext<TMsg>(
                        Func<TMsg, bool> condition, 
                        string userMessage = null) where TMsg :  ICorrelatedMessage, T
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
