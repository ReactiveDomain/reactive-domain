using ReactiveDomain.Messaging;

namespace ReactiveDomain.Bus
{


    // ReSharper disable once TypeParameterCanBeVariant
    public interface IHandleCommand<T> where T : Command
    {
        /// <summary>
        /// Cancel Commands are broadcast to all
        /// Implementors should test if the cancel is for a handled command type and a currently executing command 
        /// and store the cancel id to test incoming commands as the command and the cancel can be out of order.
        /// </summary>
        /// <param name="cancelRequest"></param>
        void RequestCancel(CancelCommand cancelRequest);
        CommandResponse Handle(T command);
    }
}
