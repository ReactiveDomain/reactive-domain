using System.Collections.Generic;
using EventStore.ClientAPI;

namespace ReactiveDomain
{
    /// <summary>
    /// Translates a <see cref="EventSourceChangeset">set of events</see> into <see cref="EventData">events</see> Event Store can persist.
    /// </summary>
    /// <remarks>The reason for this concept is so that we can translate, serialize, skip, merge, split events.</remarks>
    public interface IEventSourceChangesetTranslator
    {
        IEnumerable<EventData> Translate(EventSourceChangeset changeset);
    }
}