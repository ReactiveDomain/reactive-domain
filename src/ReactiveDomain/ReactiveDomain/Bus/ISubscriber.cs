using System;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Bus
{
    public interface ISubscriber
    {
        IDisposable Subscribe<T>(IHandle<T> handler) where T : Message;
        void Unsubscribe<T>(IHandle<T> handler) where T : Message;
        bool HasSubscriberFor<T>(bool includeDerived = false) where T : Message;
    }
}