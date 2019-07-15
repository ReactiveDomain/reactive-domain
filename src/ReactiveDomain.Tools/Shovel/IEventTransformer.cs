namespace Shovel
{
    using EventStore.ClientAPI;

    public interface IEventTransformer
    {
        ResolvedEvent Trasnform(ResolvedEvent sourceEvent);
    }
}
