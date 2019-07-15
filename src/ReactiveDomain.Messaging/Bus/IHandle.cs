// ReSharper disable TypeParameterCanBeVariant
namespace ReactiveDomain.Messaging.Bus
{
    /// <summary>
    /// Used to handle messages of type T without changing state.
    /// </summary>
    public interface IHandle<T> where T: IMessage
    {
        void Handle(T message);
    }
}