using System;

namespace ReactiveDomain.Messaging.Bus
{
    /// <summary>
    /// A General bus that is always null
    /// </summary>
    public class NullBus: IGeneralBus
    {
        public NullBus(string name = "NullBus"){Name = name;}
        public void Publish(Message message){/*null bus, just drop it*/}
        public IDisposable Subscribe<T>(IHandle<T> handler) where T : Message
        {
            throw new InvalidOperationException("Cannot subscribe to a null bus");
        }
        public void Unsubscribe<T>(IHandle<T> handler) where T : Message{/*null bus, just drop it*/}
        public bool HasSubscriberFor<T>(bool includeDerived = false) where T : Message{return false;}
        public string Name { get; }

        public void Fire(Command command, string exceptionMsg = null, TimeSpan? responseTimeout = null,
            TimeSpan? ackTimeout = null)
        {
            /*null bus, just drop it*/
        }

        public bool TryFire(Command command, out CommandResponse response, TimeSpan? responseTimeout = null,
            TimeSpan? ackTimeout = null)
        {
            response = null;
            return false;
        }

        public bool TryFire(Command command, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null)
        {
            CommandResponse response;
            return TryFire(command, out response, responseTimeout, ackTimeout);
        }

        public IDisposable Subscribe<T>(IHandleCommand<T> handler) where T : Command
        {
            throw new InvalidOperationException("Cannot subscribe to a null bus");
        }
        public void Unsubscribe<T>(IHandleCommand<T> handler) where T : Command{/*null bus, just drop it*/}

       
    }
}
