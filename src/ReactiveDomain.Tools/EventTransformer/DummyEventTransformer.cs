using EventStore.ClientAPI;
using Shovel;
using System;

namespace EventTransformer
{
    public class DummyEventTransformer : IEventTransformer
    {
        public ResolvedEvent Transform(ResolvedEvent sourceEvent)
        {
            Console.WriteLine($"Doing dummy transformation for event {sourceEvent.Event.EventId}");
            return sourceEvent;
        }
    }
}
