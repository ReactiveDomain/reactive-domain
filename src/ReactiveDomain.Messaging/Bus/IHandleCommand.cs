namespace ReactiveDomain.Messaging.Bus
{
    public interface IHandleCommand<T> where T : Command
    {
        CommandResponse Handle(T command);
    }
}
