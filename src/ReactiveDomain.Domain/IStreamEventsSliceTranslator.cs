using System.Collections.Generic;
using EventStore.ClientAPI;

namespace ReactiveDomain
{
    /// <summary>
    /// Translates a <see cref="StreamEventsSlice">slice of events</see> into events a source of events can partially restore from.
    /// </summary>
    /// <remarks>The reason for this concept is so that we can translate, deserialize, skip, merge, split events in a statefull manner.</remarks>
    public interface IStreamEventsSliceTranslator
    {
        /// <summary>
        /// Translates a <see cref="StreamEventsSlice">slice of events</see> into events a source of events can partially restore from.
        /// </summary>
        /// <param name="slice">The slice of events to translate.</param>
        /// <returns>A stream of events a source of events can partially restore from.</returns>
        IEnumerable<object> Translate(StreamEventsSlice slice);
    }
}