

using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ReactiveDomain.Testing.Specifications
{
    public sealed class TestQueueTests : 
        IHandleCommand<TestCommands.Command1>,
        IDisposable
    {
        private readonly IDispatcher _dispatcher;
        public TestQueueTests()
        {
            _dispatcher = new Dispatcher("TestQueueTest");
            _dispatcher.Subscribe<TestCommands.Command1>(this);
        }

        public CommandResponse Handle(TestCommands.Command1 command)
        {
            return command.Succeed();
        }
               
        [Fact]
        public void can_wait_on_base_types()
        {
            using (var tq = new TestQueue(_dispatcher))
            {
                var evt = new TestEvent();
                var cmd = new TestCommands.Command1();
                _dispatcher.Publish(evt);
                _dispatcher.Send(cmd);

                tq.WaitFor<IMessage>(TimeSpan.FromMilliseconds(100));
                tq.WaitFor<TestEvent>(TimeSpan.FromMilliseconds(100));
                tq.WaitFor<CommandResponse>(TimeSpan.FromMilliseconds(100));
                tq.WaitFor<Success>(TimeSpan.FromMilliseconds(100));
                tq.WaitFor<TestCommands.Command1>(TimeSpan.FromMilliseconds(100));
                Assert.Throws<TimeoutException>(() => tq.WaitFor<Fail>(TimeSpan.FromMilliseconds(10)));
                tq.AssertNext<TestEvent>(evt.CorrelationId)
                  .AssertNext<AckCommand>(cmd.CorrelationId)
                  .AssertNext<Success>(cmd.CorrelationId)
                  .AssertNext<TestCommands.Command1>(cmd.CorrelationId)
                  .AssertEmpty();
            }
        }
        [Fact]
        public void can_get_all_messages_from_queue()
        {
            using (var tq = new TestQueue(_dispatcher))
            {
                var evt = new TestEvent();
                var cmd = new TestCommands.Command1();
                _dispatcher.Publish(evt);
                _dispatcher.Send(cmd);

                tq.WaitFor<CommandResponse>(TimeSpan.FromMilliseconds(100));
                tq.WaitFor<Success>(TimeSpan.FromMilliseconds(100));
                Assert.Throws<TimeoutException>(() => tq.WaitFor<Fail>(TimeSpan.FromMilliseconds(10)));
                tq.AssertNext<TestEvent>(evt.CorrelationId)
                  .AssertNext<AckCommand>(cmd.CorrelationId)
                  .AssertNext<Success>(cmd.CorrelationId)
                  .AssertNext<TestCommands.Command1>(cmd.CorrelationId)
                  .AssertEmpty();
            }
        }

        [Fact]
        public void can_restrict_queue_to_commands()
        {
            using (var tq = new TestQueue(_dispatcher, new[] { typeof(Command) }))
            {
                var evt = new TestEvent();
                var cmd = new TestCommands.Command1();
                _dispatcher.Publish(evt);
                _dispatcher.Send(cmd);

                tq.AssertNext<TestCommands.Command1>(cmd.CorrelationId)
                  .AssertEmpty();
            }
        }

        [Fact]
        public void can_restrict_queue_to_events()
        {
            using (var tq = new TestQueue(_dispatcher, new[] { typeof(Event) }))
            {
                var evt = new TestEvent();
                var cmd = new TestCommands.Command1();
                _dispatcher.Publish(evt);
                _dispatcher.Send(cmd);

                tq.AssertNext<TestEvent>(evt.CorrelationId)
                    .AssertEmpty();
            }
        }

        [Fact]
        public void can_restrict_queue_to_commands_and_events()
        {
            using (var tq = new TestQueue(_dispatcher, new[] { typeof(Event), typeof(Command) }))
            {
                var evt = new TestEvent();
                var cmd = new TestCommands.Command1();
                _dispatcher.Publish(evt);
                _dispatcher.Send(cmd);

                tq.AssertNext<TestEvent>(evt.CorrelationId)
                  .AssertNext<TestCommands.Command1>(cmd.CorrelationId)
                  .AssertEmpty();
            }
        }
        [Fact]
        public void can_timeout_waiting_for_message_of_type()
        {
            using (var tq = new TestQueue(_dispatcher, new[] { typeof(Event), typeof(Command) }))
            {
                // Don't publish anything
                Assert.Throws<TimeoutException>(() => tq.WaitFor<TestEvent>(TimeSpan.FromMilliseconds(100)));
                tq.AssertEmpty();
            }
        }

        [Fact]
        public void can_wait_for_a_specific_message()
        {
            using (var tq = new TestQueue(_dispatcher))
            {
                var evt = new TestEvent();
                var evt2 = new TestEvent();
                _dispatcher.Publish(evt);
                _dispatcher.Publish(evt2);

                tq.WaitForMsgId(evt2.MsgId, TimeSpan.FromMilliseconds(200));

                tq.AssertNext<TestEvent>(evt.CorrelationId)
                  .AssertNext<TestEvent>(evt2.CorrelationId)
                  .AssertEmpty();
            }
        }
        [Fact]
        public void can_wait_for_a_specific_message_twice()
        {
            using (var tq = new TestQueue(_dispatcher))
            {
                var evt = new TestEvent();
                Task.Delay(50)
                    .ContinueWith( _ => _dispatcher.Publish(evt));

                Parallel.Invoke(
                    () => tq.WaitForMsgId(evt.MsgId, TimeSpan.FromMilliseconds(200)),
                    () => tq.WaitForMsgId(evt.MsgId, TimeSpan.FromMilliseconds(200))
                    );
                
                tq.AssertNext<TestEvent>(evt.CorrelationId)
                  .AssertEmpty();
            }
        }
        [Fact]
        public void can_wait_for_multiple_message_already_in_the_queue()
        {
            using (var tq = new TestQueue(_dispatcher))
            {
                var evt = new TestEvent();
                var evt2 = new TestEvent();
                _dispatcher.Publish(evt);
                _dispatcher.Publish(evt2);

                tq.WaitForMsgId(evt.MsgId, TimeSpan.FromMilliseconds(200));
                tq.WaitForMsgId(evt2.MsgId, TimeSpan.FromMilliseconds(200));

                tq.AssertNext<TestEvent>(evt.CorrelationId)
                  .AssertNext<TestEvent>(evt2.CorrelationId)
                  .AssertEmpty();
            }
        }
        [Fact]
        public void can_wait_for_multiple_messages_not_yet_recieved()
        {
            using (var tq = new TestQueue(_dispatcher))
            {
                var evt = new TestEvent();
                var evt2 = new TestEvent();
                Task.Delay(50)
                     .ContinueWith(_ =>
                     {
                         _dispatcher.Publish(evt);
                         _dispatcher.Publish(evt2);
                     });

                tq.WaitForMsgId(evt.MsgId, TimeSpan.FromMilliseconds(200));
                tq.WaitForMsgId(evt2.MsgId, TimeSpan.FromMilliseconds(200));

                tq.AssertNext<TestEvent>(evt.CorrelationId)
                  .AssertNext<TestEvent>(evt2.CorrelationId)
                  .AssertEmpty();
            }
        }
        [Fact]
        public void can_timeout_waiting_for_message_with_wrong_id()
        {
            using (var tq = new TestQueue(_dispatcher, new[] { typeof(Event), typeof(Command) }))
            {
                var evt = new TestEvent();
                _dispatcher.Publish(evt);

                Assert.Throws<TimeoutException>(() => tq.WaitForMsgId(Guid.NewGuid(), TimeSpan.FromMilliseconds(100)));

                tq.AssertNext<TestEvent>(evt.CorrelationId)
                    .AssertEmpty();
            }
        }

        public void Dispose()
        {
            _dispatcher?.Dispose();
        }
    }
}
