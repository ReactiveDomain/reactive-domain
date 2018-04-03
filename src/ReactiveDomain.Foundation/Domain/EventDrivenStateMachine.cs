using System;
using System.Collections.Generic;


namespace ReactiveDomain {
    /// <summary>
    /// The base class each process manager or aggregate's root entity should derive from.
    /// </summary>
    public abstract class EventDrivenStateMachine : IEventSource
    {
        private readonly EventRecorder _recorder;
        private readonly EventRouter _router;
        private long _version;


        public Guid Id { get; protected set; }

        public long Version => _version;
        long IEventSource.ExpectedVersion {
            get => _version;
            set => _version = value;
        }

        /// <summary>
        /// Initializes an event source's routing and recording behavior.
        /// </summary>
        protected EventDrivenStateMachine() {
            _recorder = new EventRecorder();
            _router = new EventRouter();

            _version = -1;
        }

        public void RestoreFromEvents(IEnumerable<object> events) {
            if (events == null)
                throw new ArgumentNullException(nameof(events));
            if (_recorder.HasRecordedEvents)
                throw new InvalidOperationException("Restoring from events is not possible when an instance has recorded events.");

            foreach (var @event in events) {
                RestoreFromEvent(@event);
            }
        }

        //Avoid boxing and unboxing single values
        public void RestoreFromEvent(object @event) {
            if (@event == null)
                throw new ArgumentNullException(nameof(@event));
            if (_recorder.HasRecordedEvents)
                throw new InvalidOperationException("Restoring from events is not possible when an instance has recorded events.");

            if (_version < 0) // new aggregates have a expected version of -1 or -2
                _version = 0; // got first event (zero based)
            else
                _version++;

            _router.Route(@event);
        }
        public object[] TakeEvents() {
            var records = _recorder.RecordedEvents;
            _recorder.Reset();
            return records;
        }

        /// <summary>
        /// Registers a route for the specified <typeparamref name="TEvent">type of event</typeparamref> to the logic that needs to be applied to this instance to support future behaviors.
        /// </summary>
        /// <typeparam name="TEvent">The type of event.</typeparam>
        /// <param name="route">The logic to route the event to.</param>
        protected void Register<TEvent>(Action<TEvent> route) {
            _router.RegisterRoute(route);
        }

        /// <summary>
        /// Registers a route for the specified <paramref name="typeOfEvent">type of event</paramref> to the logic that needs to be applied to this instance to support future behaviors.
        /// </summary>
        /// <param name="typeOfEvent">The type of event.</param>
        /// <param name="route">The logic to route the event to.</param>
        protected void Register(Type typeOfEvent, Action<object> route) {
            _router.RegisterRoute(typeOfEvent, route);
        }

        /// <summary>
        /// Raises the specified <paramref name="event"/> - applies it to this instance and records it in its history.
        /// </summary>
        /// <param name="event">The event to apply and record.</param>
        protected void Raise(object @event) {
            _router.Route(@event);
            _recorder.Record(@event);
        }
    }
}