using System;

namespace ReactiveDomain.Messaging.Bus
{
    /// <summary>
    /// A General bus that is always null
    /// </summary>
    public class NullBus: IDispatcher
    {
        public NullBus(string name = "NullBus"){Name = name;}
        public void Publish(Message message){/*null bus, just drop it*/}
        public bool Idle => true; //always idle
        public IDisposable Subscribe<T>(IHandle<T> handler) where T : Message
        {
            throw new InvalidOperationException("Cannot subscribe to a null bus");
        }
        public void Unsubscribe<T>(IHandle<T> handler) where T : Message{/*null bus, just drop it*/}
        public bool HasSubscriberFor<T>(bool includeDerived = false) where T : Message{return false;}
        public string Name { get; }

        public void Send(Command command, string exceptionMsg = null, TimeSpan? responseTimeout = null,
            TimeSpan? ackTimeout = null)
        {
            /*null bus, just drop it*/
        }

        public bool TrySend(Command command, out CommandResponse response, TimeSpan? responseTimeout = null,
            TimeSpan? ackTimeout = null)
        {
            response = null;
            return false;
        }

        public bool TrySendAsync(Command command, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null)
        {
            return false;
        }

        public IDisposable Subscribe<T>(IHandleCommand<T> handler) where T : Command
        {
            throw new InvalidOperationException("Cannot subscribe to a null bus");
        }
        public void Unsubscribe<T>(IHandleCommand<T> handler) where T : Command{/*null bus, just drop it*/}
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
