using System;

namespace ReactiveDomain.Messaging.Bus
{
    public interface ISubscriber
    {
        IDisposable Subscribe<T>(IHandle<T> handler) where T : Message;
        void Unsubscribe<T>(IHandle<T> handler) where T : Message;
        bool HasSubscriberFor<T>(bool includeDerived = false) where T : Message;
    }
}