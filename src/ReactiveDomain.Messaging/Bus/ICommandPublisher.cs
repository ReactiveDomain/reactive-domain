using System;

namespace ReactiveDomain.Messaging.Bus
{
    public interface ICommandPublisher
    {
        /// <summary>
        /// Send blocks the calling thread until a command response or timeout is received
        /// </summary>
        /// <param name="command">The command to send</param>
        /// <param name="exceptionMsg">The text of Exception wrapping the thrown exception, 
        ///                 useful for displaying error information in UI applications</param>
        /// <param name="responseTimeout">How long to wait for completion before throwing a timeout exception and sending a cancel</param>
        /// <param name="ackTimeout">How long to wait for processing to start before throwing a timeout exception and sending a cancel</param>
        void Send(ICommand command, string exceptionMsg =null, TimeSpan? responseTimeout = null,TimeSpan? ackTimeout = null);
        /// <summary>
        /// TrySend will block the calling thread and returns the command response via the out parameter.
        /// Will not throw, check the command response exception property on failed responses for the exception
        /// </summary>
        /// <param name="command">the command to send</param>
        /// <param name="response">the command response, of type success or fail</param>
        /// <param name="responseTimeout">How long to wait for completion before throwing a timeout exception and sending a cancel</param>
        /// <param name="ackTimeout">How long to wait for processing to start before throwing a timeout exception and sending a cancel</param>
        /// <returns>true if command response is of type Success, False if CommandResponse is of type Fail</returns>
        bool TrySend(ICommand command, out CommandResponse response, TimeSpan? responseTimeout = null,TimeSpan? ackTimeout = null);
        /// <summary>
        /// TrySendAsync will not block the calling thread. 
        /// 
        /// Useful for very long running commands, but also consider using a pair of matched messages instead. 
        /// 
        /// If handling the response is required, the caller must subscribe directly to the Command Response messages 
        /// and correlate on the message id, this can be expensive.
        /// 
        /// Using an explicitly typed set of CommandResponses may allow for the caller to process fewer CommandResponses 
        /// by subscribing explicitly.
        /// 
        /// Also consider actively subscribing and unsubscribing only for the duration of the command. 
        ///  </summary>
        /// <param name="command">the command to send</param>
        /// <param name="responseTimeout">How long to wait for completion before throwing a timeout exception and sending a cancel</param>
        /// <param name="ackTimeout">How long to wait for processing to start before throwing a timeout exception and sending a cancel</param>
        /// <returns>Returns true if the command was successfully published. N.B. this does not indicate if the command was processed or succeeded!</returns>
        bool TrySendAsync(ICommand command, TimeSpan? responseTimeout = null,TimeSpan? ackTimeout = null);
    }
}
