//using EventStore.Core.Services.Monitoring.Stats;

namespace ReactiveDomain.Messaging.Bus
{
    public interface IQueuedHandler: IHandle<IMessage>, IPublisher
    {
        string Name { get; }
        void Start();
        void Stop();
        void RequestStop();
        bool Idle { get; }
        //void Publish(Message message);
        //QueueStats GetStatistics();
    }
}