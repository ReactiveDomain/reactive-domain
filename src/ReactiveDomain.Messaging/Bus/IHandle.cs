
// ReSharper disable TypeParameterCanBeVariant
namespace ReactiveDomain.Messaging.Bus
{

    public interface IHandle<T> where T: Message
    {
        void Handle(T message);
    }
}