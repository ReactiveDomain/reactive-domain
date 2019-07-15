namespace ReactiveDomain.Messaging.Bus
{
    public interface IHandleCommand<T> where T : class, ICommand
    {
        CommandResponse Handle(T command);
    }
}
