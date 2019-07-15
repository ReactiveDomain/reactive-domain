// ReSharper disable TypeParameterCanBeVariant
namespace ReactiveDomain.Messaging.Bus
{
    /// <summary>
    /// Used to handle commands of type T. A command is usually used to request a state change.
    /// </summary>
    public interface IHandleCommand<T> where T : class, ICommand
    {
        CommandResponse Handle(T command);
    }
}
