using ReactiveDomain.Messaging;

namespace ReactiveDomain.Bus
{
    public interface IHandleCommand<T> where T : Command
    {
        CommandResponse Handle(T command);
    }
}
