
// ReSharper disable TypeParameterCanBeVariant
namespace ReactiveDomain.Messaging.Bus
{

    public interface IHandle<T> where T: IMessage
    {
        void Handle(T message);
    }
}