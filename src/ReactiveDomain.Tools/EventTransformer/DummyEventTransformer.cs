using EventStore.ClientAPI;
using Shovel;
using System;

namespace EventTransformer
{
    using System.Collections.Generic;

    public class DummyEventTransformer : IEventTransformer
    {
        public ICollection<EventData> Transform(ResolvedEvent sourceEvent)
        {
            Console.WriteLine($"Doing dummy transformation for event {sourceEvent.Event.EventId}");
            return new List<EventData> { new EventData(
                sourceEvent.Event.EventId,
                sourceEvent.Event.EventType,
                sourceEvent.Event.IsJson,
                sourceEvent.Event.Data,
                sourceEvent.Event.Metadata)
            };
        }
    }
}
