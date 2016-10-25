using System;
using ReactiveDomain.Domain;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Bus
{
    public class BusWrapper:IRouteEvents
    {
        private readonly IBus _bus;

        public BusWrapper(IBus bus)
        {
            _bus = bus;
        }

        public void Dispatch(object eventMessage)
        {
            if (eventMessage is Message)
            {
                _bus.Publish(eventMessage as Message);
            }
        }

        public void Register(IAggregate aggregate)
        {
            throw new NotImplementedException();
        }

        public void Register<T>(Action<T> handler)
        {
            throw new NotImplementedException();
        }
    }
}
