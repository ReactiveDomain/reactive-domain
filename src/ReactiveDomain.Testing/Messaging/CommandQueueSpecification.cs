using System;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Messaging.Testing
{
    public abstract class CommandQueueSpecification
    {
        public readonly MultiQueuedHandler Queue;
        protected readonly IGeneralBus Bus;
        public readonly TestQueue TestQueue;
        public ConcurrentMessageQueue<Message> BusMessages => TestQueue.Messages;
        public ConcurrentMessageQueue<Event> BusEvents => TestQueue.Events;
        public ConcurrentMessageQueue<Command> BusCommands => TestQueue.Commands;

        protected CommandQueueSpecification(
                        int queueCount = 1 ,
                        int slowCmdMs = 500,
                        int slowAckMs = 500)
        {
            
            Bus = new CommandBus(
                            "Fixture Bus",
                            slowCmdThreshold : TimeSpan.FromMilliseconds(slowCmdMs),
                            slowMsgThreshold: TimeSpan.FromMilliseconds(slowAckMs));

            Queue = new MultiQueuedHandler(
                                    queueCount,
                                    index => new QueuedHandler(
                                                    new AdHocHandler<Message>(
                                                            msg =>
                                                            {

                                                                if (msg is Command)
                                                                    Bus.TryFire((Command)msg);
                                                                else
                                                                    Bus.Publish(msg);
                                                            }), 
                                                    $"Queue {index}"
                                                 )
                                            );
            Queue.Start();
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
