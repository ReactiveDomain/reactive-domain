namespace Shovel
{
    using System.Collections.Generic;
    using EventStore.ClientAPI;

    public interface IEventTransformer
    {
        ICollection<EventData> Transform(ResolvedEvent sourceEvent);
    }
}
