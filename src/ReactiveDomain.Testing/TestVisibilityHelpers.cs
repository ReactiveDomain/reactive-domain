using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ReactiveDomain.Foundation;
using ReactiveDomain;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Testing
{
    public static class TestVisibilityExtensions
    {      
        private static T GetInstanceField<T, V>(V instance, string fieldName)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            FieldInfo field = typeof(V).GetField(fieldName, bindFlags);
            T value = (T)field?.GetValue(instance);
            if (value == null) { throw new InvalidOperationException($"{fieldName} not found on {typeof(T).Name}."); }
            return value;
        }
        private static T SetInstanceField<T>(object instance, string fieldName, T newValue)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            FieldInfo field = instance.GetType().GetField(fieldName, bindFlags);
            field?.SetValue(instance, newValue);
            T value = (T)field?.GetValue(instance);
            if (value == null) { throw new InvalidOperationException($"{fieldName} not found on {typeof(T).Name}."); }
            return value;
        }
        /// <summary>
        /// UpdateViaListener should only be used when the read model 
        /// has specialized Listener behavior and UpdateFromAggregate
        /// cannot be used.
        /// </summary>
        /// <param name="model">Read Model Under Test</param>
        /// <param name="name">Listener stream name</param>
        /// <param name="aggregate">Source Aggregate</param>
        /// <exception cref="StreamNotFoundException"></exception>
        public static void UpdateViaListener(this ReadModelBase model, string name, EventDrivenStateMachine aggregate = null)
        {
            var listeners = GetInstanceField<List<IListener>, ReadModelBase>(model, "_listeners");
            var listener = listeners.FirstOrDefault(l => l.StreamName == name);
            if (listener == null) { throw new StreamNotFoundException($"Listener {name}"); }
            if (aggregate == null) { return; }
            foreach (IMessage evt in aggregate.TakeEvents())
            {
                model.DirectApply(evt);
                SetInstanceField(listener, "_position", listener.Position + 1);
            }
        }
        /// <summary>
        /// UpdateFromAggregate should be used to test read models 
        /// in place of MockRepositories. 
        /// This will update bypassing the queue and Listeners
        /// The ReaderLock will be enforced.
        /// Unsubscribed messages will be silently dropped by the read model.
        /// </summary>
        /// <param name="model">Read Model Under Test</param>
        /// <param name="aggregate">Source Aggregate</param>
        public static void UpdateFromAggregate(this ReadModelBase model, EventDrivenStateMachine aggregate)
        {

            foreach (IMessage evt in aggregate.TakeEvents())
            {
                model.DirectApply(evt);
            }
        }

    }
}
