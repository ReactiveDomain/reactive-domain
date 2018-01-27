using System;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Tests.Helpers;

namespace ReactiveDomain.Messaging.Tests.Specifications
{
    public abstract class CommandBusSpecification
    {
        public readonly IGeneralBus Bus;
        public readonly IGeneralBus LocalBus;
        public readonly TestQueue TestQueue;
        public ConcurrentMessageQueue<Message> BusMessages => TestQueue.Messages;
        public ConcurrentMessageQueue<DomainEvent> BusEvents => TestQueue.Events;
        public ConcurrentMessageQueue<Command> BusCommands => TestQueue.Commands;

        protected CommandBusSpecification()
        {
            Bus = new CommandBus("Fixture Bus",slowMsgThreshold: TimeSpan.FromMilliseconds(500));
            LocalBus = new CommandBus("Fixture LocalBus");
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
