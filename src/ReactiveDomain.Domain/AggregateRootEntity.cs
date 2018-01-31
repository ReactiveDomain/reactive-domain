using System;
using System.Collections.Generic;
using EventStore.ClientAPI;

namespace ReactiveDomain
{
    /// <summary>
    /// The base class each aggregate's root entity should derive from.
    /// </summary>
    public abstract class AggregateRootEntity : IEventSource
    {
        private readonly EventRecorder _recorder;
        private readonly EventRouter _router;
        private long _expectedVersion;

        Guid IEventSource.Id => Id;

        //TODO: smooth this out
        public Guid Id { get; protected set; }

        /// <summary>
        /// Initializes an event source's routing and recording behavior.
        /// </summary>
        protected AggregateRootEntity()
        {
            _recorder = new EventRecorder();
            _router = new EventRouter();

            _expectedVersion = ExpectedVersion.NoStream;
        }

        long IEventSource.ExpectedVersion
        {
            get => _expectedVersion;
            set => _expectedVersion = value;
        }

        void IEventSource.RestoreFromEvents(IEnumerable<object> events)
        {
            if (events == null)
                throw new ArgumentNullException(nameof(events));
            if (_recorder.HasRecordedEvents)
                throw new InvalidOperationException("Restoring from events is not possible when an instance has recorded events.");
         
            foreach (var @event in events)
            {
                RestoreFromEvent(@event);
            }
          }
        //Avoid boxing and unboxing single values
        void IEventSource.RestoreFromEvent(object @event)
        {
            RestoreFromEvent(@event);
        }
        private void RestoreFromEvent(object @event)
        {
            if (@event == null)
                throw new ArgumentNullException(nameof(@event));
            if (_recorder.HasRecordedEvents)
                throw new InvalidOperationException("Restoring from events is not possible when an instance has recorded events.");
            
            if (_expectedVersion < 0) // new aggregates have a expected version of -1 or -2
                _expectedVersion = 0; // got first event (zero based)
            else
                _expectedVersion++;
            _router.Route(@event);
        }
        object[] IEventSource.TakeEvents()
        {
            var records = _recorder.RecordedEvents;
            _recorder.Reset();
            return records;
        }

        /// <summary>
        /// Registers a route for the specified <typeparamref name="TEvent">type of event</typeparamref> to the logic that needs to be applied to this instance to support future behaviors.
        /// </summary>
        /// <typeparam name="TEvent">The type of event.</typeparam>
        /// <param name="route">The logic to route the event to.</param>
        protected void Register<TEvent>(Action<TEvent> route)
        {
            _router.RegisterRoute(route);
        }

        /// <summary>
        /// Registers a route for the specified <paramref name="typeOfEvent">type of event</paramref> to the logic that needs to be applied to this instance to support future behaviors.
        /// </summary>
        /// <param name="typeOfEvent">The type of event.</typeparam>
        /// <param name="route">The logic to route the event to.</param>
        protected void Register(Type typeOfEvent, Action<object> route)
        {
            _router.RegisterRoute(typeOfEvent, route);
        }

        /// <summary>
        /// Raises the specified <paramref name="event"/> - applies it to this instance and records it in its history.
        /// </summary>
        /// <param name="event">The event to apply and record.</param>
        protected void Raise(object @event)
        {
            _router.Route(@event);
            _recorder.Record(@event);
        }
    }
}