namespace Shovel
{
    using System.Collections.Generic;
    using EventStore.ClientAPI;

    public interface IEventTransformer
    {
        ICollection<ResolvedEvent> Transform(ResolvedEvent sourceEvent);
    }
}
