using System;
using System.Collections.Generic;

namespace ReactiveDomain
{
    /// <summary>
    /// Represents a source of events from the perspective of restoring from and taking events. To be used by infrastructure code only.
    /// </summary>
    public interface IEventSource
    {
        /// <summary>
        /// Gets the unique identifier for this EventSource
        /// This must be provided by the implementing class
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// Gets or Sets the expected version this instance is at.
        /// </summary>
        //todo: remove the set option here
        long ExpectedVersion { get; set; }

        /// <summary>
        /// Restores this instance from the history of events.
        /// </summary>
        /// <param name="events">The events to restore from.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="events"/> is <c>null</c>.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when this instance has already recorded events.</exception>
        void RestoreFromEvents(IEnumerable<object> events);
        
        /// <summary>
        /// Takes the recorded history of events from this instance (CQS violation, beware).
        /// </summary>
        /// <returns>The recorded events.</returns>
        object[] TakeEvents();
    }
}