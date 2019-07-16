using System;

namespace ReactiveDomain.Messaging.Bus
{
    /// <summary>
    /// A General bus that is always null
    /// useful for providing an output target that simply discards
    /// </summary>
    public class NullBus: IDispatcher
    {
        public NullBus(string name = "NullBus"){Name = name;}
        public void Publish(IMessage message){/*null bus, just drop it*/}
        public bool Idle => true; //always idle
        public IDisposable Subscribe<T>(IHandle<T> handler, bool includeDerived = true) where T : class,IMessage
        {
            throw new InvalidOperationException("Cannot subscribe to a null bus");
        }
        public IDisposable SubscribeToAll(IHandle<IMessage> handler)
        {
            throw new InvalidOperationException("Cannot subscribe to a null bus");
        }
        public void Unsubscribe<T>(IHandle<T> handler) where T : class, IMessage {/*null bus, just drop it*/}
        public bool HasSubscriberFor<T>(bool includeDerived = false) where T : class, IMessage { return false;}
        public string Name { get; }

        public void Send(ICommand command, string exceptionMsg = null, TimeSpan? responseTimeout = null,
            TimeSpan? ackTimeout = null)
        {
            /*null bus, just drop it*/
        }

        public bool TrySend(ICommand command, out CommandResponse response, TimeSpan? responseTimeout = null,
            TimeSpan? ackTimeout = null)
        {
            response = null;
            return false;
        }

        public bool TrySendAsync(ICommand command, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null)
        {
            return false;
        }

        public IDisposable Subscribe<T>(IHandleCommand<T> handler) where T : class, ICommand
        {
            throw new InvalidOperationException("Cannot subscribe to a null bus");
        }
        public void Unsubscribe<T>(IHandleCommand<T> handler) where T : class, ICommand {/*null bus, just drop it*/}
        public void Dispose(){  
            Dispose(true);  
            GC.SuppressFinalize(this);  
        }  
        protected virtual void  Dispose(bool disposing) {
            if (disposing) {
               //null bus,just drop ignore it
            }
        }
       
    }
}
