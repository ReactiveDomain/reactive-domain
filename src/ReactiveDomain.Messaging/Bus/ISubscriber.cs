using System;

namespace ReactiveDomain.Messaging.Bus
{
    public interface ISubscriber
    {
        /// <summary>
        /// Register to be called when a message is published of the type T or
        /// of a derived type from T
        /// </summary>
        /// <typeparam name="T">the type to be notified of</typeparam>
        /// <param name="handler">The object implementing IHandle T indicating function to be called</param>
        /// <param name="includeDerived">Register handlers on derived types</param>
        /// <returns>IDisposable wrapper to calling Dispose on the wrapper will unsubscribe</returns>
        IDisposable Subscribe<T>(IHandle<T> handler, bool includeDerived = true) where T : class, IMessage;

        /// <summary>
        /// Register to be called when any message is published 
        /// </summary>
        /// <param name="handler">The object implementing IHandle IMessage indicating function to be called</param>
        /// <returns>IDisposable wrapper to calling Dispose on the wrapper will unsubscribe</returns>
        IDisposable SubscribeToAll(IHandle<IMessage> handler);
        /// <summary>
        /// Unregister being called when a message is published of the type T or
        /// of a derived type from T 
        /// </summary>
        /// <typeparam name="T">the type notified</typeparam>
        /// <param name="handler">The object implementing IHandle T indicating function to be called</param>
        void Unsubscribe<T>(IHandle<T> handler) where T : class, IMessage;
        /// <summary>
        /// Returns true if this publisher has a subscription for this type of any sort
        /// </summary>
        /// <typeparam name="T">the type to check</typeparam>
        /// <param name="includeDerived">return true if this or any derived types are registered</param>
        /// <returns></returns>
        bool HasSubscriberFor<T>(bool includeDerived = false) where T : class, IMessage;
    }
}