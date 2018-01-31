using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Messaging.Tests.Helpers
{
    public class TestMessagePublisher
    {
        private readonly IGeneralBus _bus;
        private bool _publish = true;

        public TestMessagePublisher(IGeneralBus bus)
        {
            _bus = bus;
        }

        /// <summary>
        /// Publishes TestMessages in a loop at a given time interval
        /// </summary>
        /// <param name="intervalInMs"></param>
        public void StartPublishing(int intervalInMs)
        {
            Task.Run(
                () =>
                {
                    while (_publish)
                    {
                        _bus.Publish(new TestMessage());
                        if (intervalInMs > 0) Thread.Sleep(intervalInMs);
                    }
                });
        }


        /// <summary>
        /// Set the loop control variable to stop publishing.
        /// </summary>
        public void StopPublishing()
        {
            _publish = false;
        }
    }
}
