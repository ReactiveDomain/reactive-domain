namespace Shovel
{
    using EventStore.ClientAPI;

    public interface IEventTransformer
    {
        ResolvedEvent Transform(ResolvedEvent sourceEvent);
    }
}
