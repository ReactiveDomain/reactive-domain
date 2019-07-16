using EventStore.ClientAPI;
using Shovel;
using System;

namespace EventTransformer
{
    using System.Collections.Generic;

    public class DummyEventTransformer : IEventTransformer
    {
        public ICollection<ResolvedEvent> Transform(ResolvedEvent sourceEvent)
        {
            Console.WriteLine($"Doing dummy transformation for event {sourceEvent.Event.EventId}");
            return new List<ResolvedEvent>() {sourceEvent};
        }
    }
}
