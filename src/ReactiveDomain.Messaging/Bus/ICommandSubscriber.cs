using System;

namespace ReactiveDomain.Messaging.Bus
{
    public interface ICommandSubscriber
    {
        /// <summary>
        /// Set up a command-handler for the specified (T) command.  The command-handler must be
        /// declared with IHandleCommand for T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler"></param>
        IDisposable Subscribe<T>(IHandleCommand<T> handler) where T : class, ICommand;
        /// <summary>
        /// Remove up a command-handler for the specified (T) command.  The command-handler must be
        /// declared with IHandleCommand for T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler"></param>
        void Unsubscribe<T>(IHandleCommand<T> handler) where T : class, ICommand;
    }
}