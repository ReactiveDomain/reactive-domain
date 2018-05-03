using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain
{
    /// <summary>
    /// Records events on behalf of an event source.
    /// </summary>
    public class EventRecorder
    {
        private readonly List<object> _recorded;

        /// <summary>
        /// Initializes a new event recorder.
        /// </summary>
        public EventRecorder()
        {
            _recorded = new List<object>();
        }

        /// <summary>
        /// Indicates whether this instance has recorded events.
        /// </summary>
        public bool HasRecordedEvents => _recorded.Count != 0;

        /// <summary>
        /// The events recorded by the event source that holds on to this instance.
        /// </summary>
        public object[] RecordedEvents => _recorded.ToArray();

        /// <summary>
        /// Records an event on this instance.
        /// </summary>
        /// <param name="event">The event to record.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="event"/> is <c>null</c>.</exception>
        public void Record(object @event)
        {
            if (@event == null)
                throw new ArgumentNullException(nameof(@event));

            _recorded.Add(@event);
        }

        /// <summary>
        /// Resets this instance to its starting point or the point it was last reset on, effectively forgetting all events that have been recorded in the meantime.
        /// </summary>
        public void Reset()
        {
            _recorded.Clear();
        }
    }
}
