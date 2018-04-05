using System;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Messaging.Testing
{
    public abstract class CommandBusSpecification
    {
        public readonly IGeneralBus Bus;
        public readonly IGeneralBus LocalBus;
        public readonly TestQueue TestQueue;
        public ConcurrentMessageQueue<Message> BusMessages => TestQueue.Messages;
        public ConcurrentMessageQueue<Event> BusEvents => TestQueue.Events;
        public ConcurrentMessageQueue<Command> BusCommands => TestQueue.Commands;

        protected CommandBusSpecification(IGeneralBus bus = null)
        {
            Bus = bus ?? new CommandBus("Fixture Bus", 3, false, TimeSpan.FromMilliseconds(5000), TimeSpan.FromMilliseconds(5000));
            LocalBus = new CommandBus("Fixture LocalBus", 3, false, TimeSpan.FromMilliseconds(5000), TimeSpan.FromMilliseconds(5000));
            TestQueue = new TestQueue(Bus);
            try
            {
                Given();
                When();
            }
            catch (Exception)
            {
                throw;
            }
        }
        protected abstract void Given();
        protected abstract void When();


    }
}
