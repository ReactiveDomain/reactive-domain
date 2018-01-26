using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Tests.Helpers;
using Xunit;

namespace ReactiveDomain.Tests.Subscribers.QueuedSubscriber
{
    // ReSharper disable once InconsistentNaming
    public class can_handle_multiple_publisher_threads_queued : when_using_counted_message_subscriber
    {
        private int FirstTaskMax = 100000;
        private int SecondTaskMax = 200000;
        private int ThirdTaskMax = 300000;
        private int FourthTaskMax = 400000;

        private Task _t1;
        private Task _t2;
        private Task _t3;
        private Task _t4;
        protected override void When()
        {
            _t1 = new Task(
                () =>
                {
                    for (int i = 0; i < FirstTaskMax; i++)
                    {
                        Bus.Publish(new CountedTestMessage(i));
                    }
                });

            _t2 = new Task(
                () =>
                {
                    for (int i = FirstTaskMax; i < SecondTaskMax; i++)
                    {
                        Bus.Publish(new CountedTestMessage(i));
                    }
                });
            _t3 = new Task(
                () =>
                {
                    for (int i = SecondTaskMax; i < ThirdTaskMax; i++)
                    {
                        Bus.Publish(new CountedTestMessage(i));
                    }
                });
            _t4 = new Task(
                () =>
                {
                    for (int i = ThirdTaskMax; i < FourthTaskMax; i++)
                    {
                        Bus.Publish(new CountedTestMessage(i));
                    }
                });

            _t1.Start();
            _t2.Start();
            _t3.Start();
            _t4.Start();
        }

        [Fact]
        void can_handle_threaded_messages()
        {
            Assert.IsOrBecomesTrue(
                () => BusMessages.Count == FourthTaskMax,
                FourthTaskMax,
                $"Expected message count to be {FourthTaskMax} Messages, found {BusMessages.Count }");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref _messageSubscriber.MessagesHandled) == FourthTaskMax,
                FourthTaskMax,
                $"Expected {FourthTaskMax} Messages, found {_messageSubscriber.MessagesHandled }");

        }

    }
}
