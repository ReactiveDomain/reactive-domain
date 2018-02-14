using System;
using System.Diagnostics;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Bus;
using ReactiveDomain.Messaging;
using ReactiveDomain.Tests.Helpers;
using Xunit;

namespace ReactiveDomain.Tests
{
    // ReSharper disable once InconsistentNaming
    public class when_sending_commands
    {
        [Fact]
        public void publish_publishes_command_as_message()
        {
            var bus = new CommandBus("temp");
            long gotIt = 0;
            var hndl =
                new AdHocHandler<TestCommands.TestCommand>(
                    cmd => Interlocked.Exchange(ref gotIt, 1));
            bus.Subscribe(hndl);

            bus.Publish(new TestCommands.TestCommand(Guid.NewGuid(), null));

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotIt) == 1);
        }
        [Fact]
        public void fire_publishes_command_as_message()
        {
            var bus = new CommandBus("temp");
            long gotIt = 0;
            bus.Subscribe(new AdHocHandler<Command>(
                cmd => { if (cmd is TestCommands.TestCommand) Interlocked.Exchange(ref gotIt, 1); }));
            bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand>(cmd => true));
            bus.Fire(new TestCommands.TestCommand(Guid.NewGuid(), null));
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotIt) == 1);
        }

        [Fact]
        public void command_handler_acks_command_message()
        {
            var bus = new CommandBus("temp");
            long gotCmd = 0;
            long gotAck = 0;
            var hndl =
                new AdHocCommandHandler<TestCommands.TestCommand>(
                    cmd => Interlocked.Exchange(ref gotCmd, 1) == 0);
            bus.Subscribe(hndl);
            var ackHndl =
                new AdHocHandler<AckCommand>(
                    cmd => Interlocked.Exchange(ref gotAck, 1));
            bus.Subscribe(ackHndl);
            bus.Fire(new TestCommands.TestCommand(Guid.NewGuid(), null));
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotAck) == 1);
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotCmd) == 1);
        }
        [Fact]
        public void command_handler_respondes_to_command_message()
        {
            var bus = new CommandBus("temp");
            long gotCmd = 0;
            long gotResponse = 0;
            long gotAck = 0;
            long msgCount = 0;

            bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand>(
                    cmd =>
                    {
                        Interlocked.Exchange(ref gotCmd, 1);
                        return true;
                    }));

            bus.Subscribe(new AdHocHandler<CommandResponse>(
                    cmd => Interlocked.Exchange(ref gotResponse, 1)));

            bus.Subscribe(new AdHocHandler<AckCommand>(
                             cmd => Interlocked.Exchange(ref gotAck, 1)));

            bus.Subscribe(new AdHocHandler<Message>(
                            cmd => Interlocked.Increment(ref msgCount)));

            bus.Fire(new TestCommands.TestCommand(Guid.NewGuid(), null));

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref msgCount) == 3, null, "Expected 3 Messages");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotAck) == 1, null, "Expected Ack");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotCmd) == 1, null, "Expected Command was handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotResponse) == 1, null, "Expected Response");
        }

        [Fact]
        public void fire_passing_command_should_pass()
        {
            var bus = new CommandBus("local");

            long gotSuccess = 0;
            long gotFail = 0;
            long gotCancel = 0;
            bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand>(cmd => true));
            bus.Subscribe(new AdHocHandler<Success>(cmd => Interlocked.Exchange(ref gotSuccess, 1)));
            bus.Subscribe(new AdHocHandler<Fail>(cmd => Interlocked.Exchange(ref gotFail, 1)));
            bus.Subscribe(new AdHocHandler<Canceled>(cmd => Interlocked.Exchange(ref gotCancel, 1)));
            bus.Fire(new TestCommands.TestCommand(Guid.NewGuid(), null));
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotSuccess) == 1, null, "Expected Success");
            Assert.True(gotFail == 0, "Unexpected fail received.");
            Assert.True(gotCancel == 0, "Unexpected Cancel received.");
        }
        [Fact]
        public void fire_failing_command_should_fail()
        {
            var bus = new CommandBus("local");
            long gotFail = 0;
            bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand>(cmd => false));
            bus.Subscribe(new AdHocHandler<Fail>(cmd => Interlocked.Exchange(ref gotFail, 1)));
            Assert.IsOrBecomesTrue(() => bus.HasSubscriberFor<TestCommands.TestCommand>());
            Assert.Throws<CommandException>(
                () => bus.Fire(new TestCommands.TestCommand(Guid.NewGuid(), null)));
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotFail) == 1, null, "Expected Fail");
        }
        [Fact]
        public void tryfire_passing_command_should_pass()
        {
            var bus = new CommandBus("local");
            long gotSuccess = 0;
            bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand>(cmd => true));
            bus.Subscribe(new AdHocHandler<Success>(cmd => Interlocked.Exchange(ref gotSuccess, 1)));
            CommandResponse response;
            var pass = bus.TryFire(new TestCommands.TestCommand(Guid.NewGuid(), null), out response);
            Assert.True(pass, "Expected true return value");
            Assert.IsType(typeof(Success), response);
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotSuccess) == 1, null, "Expected Success");
        }
        [Fact]
        public void tryfire_failing_command_should_fail()
        {
            var bus = new CommandBus("local");
            long gotFail = 0;
            bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand>(cmd => false));
            bus.Subscribe(new AdHocHandler<Fail>(cmd => Interlocked.Exchange(ref gotFail, 1)));
            CommandResponse response;
            var pass = bus.TryFire(new TestCommands.TestCommand(Guid.NewGuid(), null), out response);
            Assert.False(pass, "Expected false return value");
            Assert.IsType(typeof(Fail), response);
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotFail) == 1, null, "Expected Fail");
        }
        
        [Fact]
        public void handlers_that_wrap_exceptions_rethrow_on_fire()
        {
            var bus = new CommandBus("local");

            long gotCmd = 0;
            long gotResponse = 0;
            long gotAck = 0;
            long msgCount = 0;

            bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand>(
                    cmd =>
                    {
                        Interlocked.Exchange(ref gotCmd, 1);
                        throw new CommandException("I knew this would happen.", cmd);
                    }));

            bus.Subscribe(new AdHocHandler<CommandResponse>(
                    cmd => Interlocked.Exchange(ref gotResponse, 1)));

            bus.Subscribe(new AdHocHandler<AckCommand>(
                             cmd => Interlocked.Exchange(ref gotAck, 1)));

            bus.Subscribe(new AdHocHandler<Message>(
                            cmd => Interlocked.Increment(ref msgCount)));

            Assert.Throws<CommandException>(() =>
                            bus.Fire(new TestCommands.TestCommand(Guid.NewGuid(), null)));

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref msgCount) == 3, null, "Expected 3 Messages");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotAck) == 1, null, "Expected Ack");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotCmd) == 1, null, "Expected Command was handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotResponse) == 1, null, "Expected Response");

        }
        [Fact]
        public void handlers_that_throw_exceptions_rethrow_on_fire()
        {
            var bus = new CommandBus("local");

            long gotCmd = 0;
            long gotResponse = 0;
            long gotAck = 0;
            long msgCount = 0;

            bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand>(
                    cmd =>
                    {
                        Interlocked.Exchange(ref gotCmd, 1);
                        throw new CommandException("I knew this would happen.", cmd);
                    },
                    wrapExceptions: false));

            bus.Subscribe(new AdHocHandler<CommandResponse>(
                    cmd => Interlocked.Exchange(ref gotResponse, 1)));

            bus.Subscribe(new AdHocHandler<AckCommand>(
                             cmd => Interlocked.Exchange(ref gotAck, 1)));

            bus.Subscribe(new AdHocHandler<Message>(
                            cmd => Interlocked.Increment(ref msgCount)));

            Assert.Throws<CommandException>(() =>
                            bus.Fire(new TestCommands.TestCommand(Guid.NewGuid(), null)));

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref msgCount) == 3, null, "Expected 3 Messages");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotAck) == 1, null, "Expected Ack");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotCmd) == 1, null, "Expected Command was handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotResponse) == 1, null, "Expected Response");

        }

        [Fact]
        public void handlers_that_wrap_exceptions_return_on_tryfire()
        {
            var bus = new CommandBus("local");

            long gotCmd = 0;
            long gotResponse = 0;
            long gotAck = 0;
            long msgCount = 0;
            const string errText = @"I knew this would happen.";
            bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand>(
                    cmd =>
                    {
                        Interlocked.Exchange(ref gotCmd, 1);
                        throw new CommandException(errText, cmd);
                    },
                    // ReSharper disable once RedundantArgumentDefaultValue
                    wrapExceptions: true));

            bus.Subscribe(new AdHocHandler<CommandResponse>(
                    cmd => Interlocked.Exchange(ref gotResponse, 1)));

            bus.Subscribe(new AdHocHandler<AckCommand>(
                             cmd => Interlocked.Exchange(ref gotAck, 1)));

            bus.Subscribe(new AdHocHandler<Message>(
                            cmd => Interlocked.Increment(ref msgCount)));

            CommandResponse response;
            var command = new TestCommands.TestCommand(Guid.NewGuid(), null);
            var passed = bus.TryFire(command, out response);

            Assert.False(passed, "Expected false return");
            Assert.IsType(typeof(Fail), response);
            Assert.IsType(typeof(CommandException), ((Fail)response).Exception);

            Assert.True(string.Equals($"{command.GetType().Name}: {errText}", ((Fail)response).Exception.Message, StringComparison.Ordinal));


            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref msgCount) == 3, null, "Expected 3 Messages");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotAck) == 1, null, "Expected Ack");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotCmd) == 1, null, "Expected Command was handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotResponse) == 1, null, "Expected Response");

        }
        [Fact]
        public void handlers_that_throw_exceptions_return_on_tryfire()
        {
            var bus = new CommandBus("local");

            long gotCmd = 0;
            long gotResponse = 0;
            long gotAck = 0;
            long msgCount = 0;
            const string errText = @"I knew this would happen.";
            bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand>(
                    cmd =>
                    {
                        Interlocked.Exchange(ref gotCmd, 1);
                        throw new CommandException(errText, cmd);
                    },
                    wrapExceptions: false));

            bus.Subscribe(new AdHocHandler<CommandResponse>(
                    cmd => Interlocked.Exchange(ref gotResponse, 1)));

            bus.Subscribe(new AdHocHandler<AckCommand>(
                             cmd => Interlocked.Exchange(ref gotAck, 1)));

            bus.Subscribe(new AdHocHandler<Message>(
                            cmd => Interlocked.Increment(ref msgCount)));

            CommandResponse response;
            var command = new TestCommands.TestCommand(Guid.NewGuid(), null);
            var passed = bus.TryFire(command, out response);

            Assert.False(passed, "Expected false return");
            Assert.IsType(typeof(Fail), response);
            Assert.IsType(typeof(CommandException), ((Fail)response).Exception);
            Assert.Equal($"{command.GetType().Name}: {errText}", ((Fail)response).Exception.Message);

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref msgCount) == 3, null, "Expected 3 Messages");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotAck) == 1, null, "Expected Ack");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotCmd) == 1, null, "Expected Command was handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotResponse) == 1, null, "Expected Response");

        }

        [Fact]
        public void typed_fire_passing_command_should_pass()
        {
            var bus = new CommandBus("local");
            const int data = 12356;

            long gotResponse = 0;
            long responseData = 0;
            bus.Subscribe(new AdHocTypedCommandHandler<TestCommands.TypedTestCommand, TestCommands.TestCommandResponse>(
                cmd => new TestCommands.TestCommandResponse(cmd, data)));
            bus.Subscribe(new AdHocHandler<TestCommands.TestCommandResponse>(
                cmd =>
                {
                    Interlocked.Exchange(ref gotResponse, 1);
                    Interlocked.Exchange(ref responseData, cmd.Data);
                }
                ));
            Assert.IsOrBecomesTrue(() => bus.HasSubscriberFor<TestCommands.TypedTestCommand>());

            bus.Fire(new TestCommands.TypedTestCommand(Guid.NewGuid(), null));
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotResponse) == 1, null, "Expected Success");
            Assert.IsOrBecomesTrue(() => data == responseData);
        }
        [Fact]
        public void typed_tryfire_passing_command_should_pass()
        {
            var bus = new CommandBus("local");
            const int data = 12356;

            long gotResponse = 0;
            long responseData = 0;
            bus.Subscribe(new AdHocTypedCommandHandler<TestCommands.TypedTestCommand, TestCommands.TestCommandResponse>(
                cmd => new TestCommands.TestCommandResponse(cmd, data)));
            bus.Subscribe(new AdHocHandler<TestCommands.TestCommandResponse>(
                cmd =>
                {
                    Interlocked.Exchange(ref gotResponse, 1);
                    Interlocked.Exchange(ref responseData, cmd.Data);
                }
                ));

            CommandResponse response;
            var pass = bus.TryFire(new TestCommands.TypedTestCommand(Guid.NewGuid(), null), out response);
            Assert.True(pass, "Expected true return value");
            Assert.IsType(typeof(TestCommands.TestCommandResponse), response);
            Assert.Equal(data, ((TestCommands.TestCommandResponse)response).Data);
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotResponse) == 1, null, "Expected Success");
            Assert.IsOrBecomesTrue(() => data == responseData);
        }
        [Fact]
        public void typed_failing_fire_command_should_fail()
        {
            var bus = new CommandBus("local");
            const int data = 12356;
            const string errText = @"I knew this would happen.";
            long gotResponse = 0;
            long responseData = 0;
            bus.Subscribe(new AdHocTypedCommandHandler<TestCommands.TypedTestCommand, TestCommands.TestFailedCommandResponse>(
                cmd => new TestCommands.TestFailedCommandResponse(cmd, new CommandException(errText, cmd), data)));
            bus.Subscribe(new AdHocHandler<TestCommands.TestFailedCommandResponse>(
                cmd =>
                {
                    Interlocked.Exchange(ref gotResponse, 1);
                    Interlocked.Exchange(ref responseData, cmd.Data);
                }
                ));

            Assert.Throws<CommandException>(() =>
                         bus.Fire(new TestCommands.TypedTestCommand(Guid.NewGuid(), null)));

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotResponse) == 1, null, "Expected Fail");
            Assert.IsOrBecomesTrue(() => data == responseData);
        }
        [Fact]
        public void typed_failing_tryfire_command_should_fail()
        {
            var bus = new CommandBus("local");
            const int data = 12356;
            const string errText = @"I knew this would happen.";
            long gotResponse = 0;
            long responseData = 0;
            bus.Subscribe(new AdHocTypedCommandHandler<TestCommands.TypedTestCommand, TestCommands.TestFailedCommandResponse>(
                cmd => new TestCommands.TestFailedCommandResponse(cmd, new CommandException(errText, cmd), data)));
            bus.Subscribe(new AdHocHandler<TestCommands.TestFailedCommandResponse>(
                cmd =>
                {
                    Interlocked.Exchange(ref gotResponse, 1);
                    Interlocked.Exchange(ref responseData, cmd.Data);
                }
                ));

            CommandResponse response;
            var command = new TestCommands.TypedTestCommand(Guid.NewGuid(), null);
            var passed = bus.TryFire(command, out response);

            Assert.False(passed, "Expected false return");
            Assert.IsType(typeof(TestCommands.TestFailedCommandResponse), response);
            Assert.IsType(typeof(CommandException), ((Fail)response).Exception);
            Assert.Equal($"{command.GetType().Name}: {errText}", ((Fail)response).Exception.Message);
            Assert.Equal(data, ((TestCommands.TestFailedCommandResponse)response).Data);
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotResponse) == 1, null, "Expected Fail");
            Assert.IsOrBecomesTrue(() => data == responseData);
        }
        [Fact]
        public void multiple_commands_can_register()
        {
            var bus = new CommandBus("local");
            long gotCmd1 = 0;
            long gotCmd2 = 0;
            bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand>(
                   cmd =>
                   {
                       Interlocked.Exchange(ref gotCmd1, 1);
                       return true;
                   }));
            bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand2>(
                   cmd =>
                   {
                       Interlocked.Exchange(ref gotCmd2, 1);
                       return true;
                   }));
            CommandResponse result;
            bus.TryFire(new TestCommands.TestCommand(Guid.NewGuid(), null), out result);
            Assert.True(result is Success);
            bus.TryFire(new TestCommands.TestCommand2(Guid.NewGuid(), null), out result);
            Assert.True(result is Success);
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotCmd1) == 1, msg: "Expected Cmd1 handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotCmd2) == 1, msg: "Expected Cmd2 handled");
        }
        [Fact]
        public void cannot_subscribe_twice_on_same_bus()
        {
            var bus = new CommandBus("local");
            bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand>(cmd => true));
            Assert.True(bus.HasSubscriberFor<TestCommands.TestCommand>());
            Assert.Throws<ExistingHandlerException>(
                () => bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand>(cmd => true)));
        }
        [Fact]
        public void commands_should_not_deadlock()
        {

            var bus = new CommandBus("local");
            long gotCmd1 = 0;
            long gotCmd2 = 0;
            bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand>(
                   cmd =>
                   {
                       Interlocked.Exchange(ref gotCmd1, 1);
                       bus.TryFire(new TestCommands.TestCommand2(Guid.NewGuid(), null));
                       return true;
                   }));
            bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand2>(
                   cmd =>
                   {
                       Interlocked.Exchange(ref gotCmd2, 1);
                       return true;
                   }));
            CommandResponse result;
            bus.TryFire(new TestCommands.TestCommand(Guid.NewGuid(), null), out result);

            Assert.True(result is Success,$"Got Fail: {(result as Fail)?.Exception.Message}");

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotCmd1) == 1, msg: "Expected Cmd1 handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotCmd2) == 1, msg: "Expected Cmd2 handled");
        }
        [Fact(Skip = "Distributed commands are currently disabled")]
        public void passing_commands_on_connected_busses_should_pass()
        {
            var bus = new CommandBus("local");
            var bus2 = new CommandBus("remote");
            var conn = new BusConnector(bus, bus2);
            long gotCmd1 = 0;

            bus2.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand>(
                   cmd =>
                   {
                       Interlocked.Exchange(ref gotCmd1, 1);
                       return true;
                   }));

            CommandResponse result;
            bus.TryFire(new TestCommands.TestCommand(Guid.NewGuid(), null), out result);
            Assert.True(result is Success);

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotCmd1) == 1, msg: "Expected Cmd1 handled");

        }

        [Fact]
        public void unsubscribed_commands_should_throw_ack_timeout()
        {
            var bus = new CommandBus("local");
            long publishedMessageCount = 0;
            bus.Subscribe(new AdHocHandler<Message>(c => Interlocked.Increment(ref publishedMessageCount)));
            Assert.Throws<CommandNotHandledException>(() =>
                 bus.Fire(new TestCommands.TestCommand(Guid.NewGuid(), null)));

            Assert.Equal(0, Interlocked.Read(ref publishedMessageCount), userMessage: "Expected no messages published");
        }
       
        [Fact]
        public void tryfire_unsubscribed_commands_should_return_throw_commandNotHandledException()
        {
            var bus = new CommandBus("local");
            CommandResponse response;
            var passed = bus.TryFire(new TestCommands.TestCommand(Guid.NewGuid(), null), out response);

            Assert.False(passed, "Expected false return");
            Assert.IsType(typeof(Fail), response);
            Assert.IsType(typeof(CommandNotHandledException), ((Fail)response).Exception);
        }
        [Fact]
        public void tryfire_slow_commands_should_return_timeout()
        {
            var bus = new CommandBus("local");
            long gotCmd1 = 0;

            bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand>(
                   cmd =>
                   {
                       Interlocked.Exchange(ref gotCmd1, 1);
                       Task.Delay(1000).Wait();
                       return true;
                   }));

            CommandResponse response;
            var passed = bus.TryFire(new TestCommands.TestCommand(Guid.NewGuid(), null), out response);

            Assert.False(passed, "Expected false return");
            Assert.IsType(typeof(Fail), response);//,$"Expected 'Fail' got {response.GetType().Name}");
            Assert.IsType(typeof(CommandTimedOutException), ((Fail)response).Exception);
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotCmd1) == 1, msg: "Expected Cmd1 handled");

        }

        [Fact(Skip = "Connected bus scenarios currently disabled")]
        public void tryfire_oversubscribed_commands_should_return_false()
        {
            var bus = new CommandBus("local");
            var bus2 = new CommandBus("remote");
            var conn = new BusConnector(bus, bus2);
            long gotCmd = 0;
            long proccessedCmd = 0;
            long cancelPublished = 0;
            long cancelReturned = 0;

            bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand>(
                 cmd =>
                 {
                     Interlocked.Increment(ref gotCmd);
                     Task.Delay(250).Wait();
                     Interlocked.Increment(ref proccessedCmd);
                     return true;
                 }));
            bus2.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand>(
                    cmd =>
                    {
                        Interlocked.Increment(ref gotCmd);
                        Task.Delay(250).Wait();
                        Interlocked.Increment(ref proccessedCmd);
                        return true;
                    }));
            bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand2>(
                 cmd =>
                 {
                     Interlocked.Increment(ref gotCmd);
                     Task.Delay(250).Wait();
                     Interlocked.Increment(ref proccessedCmd);
                     return true;
                 }));
            bus2.Subscribe(new AdHocHandler<Canceled>(c => Interlocked.Increment(ref cancelPublished)));
            bus2.Subscribe(new AdHocHandler<Fail>(
                c =>
                {
                    if (c.Exception is CommandCanceledException)
                        Interlocked.Increment(ref cancelReturned);
                }));
            CommandResponse response;
            var timer = Stopwatch.StartNew();
            var passed = bus.TryFire(
                new TestCommands.TestCommand(Guid.NewGuid(), null), out response, TimeSpan.FromMilliseconds(1500));
            timer.Stop();
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotCmd) <= 1,
                msg: "Expected command received no more than once, got " + gotCmd);
            Assert.True(timer.ElapsedMilliseconds < 1000, "Expected failure before task completion.");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref proccessedCmd) == 0,
                 msg: "Expected command failed before handled; got " + proccessedCmd);

            Assert.False(passed, "Expected false return");
            Assert.IsType(typeof(Fail), response);
            Assert.IsType(typeof(CommandOversubscribedException), ((Fail)response).Exception);
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref proccessedCmd) <= 1, 1500, msg: "Expected command handled no more than once, actual" + proccessedCmd);
            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref cancelPublished) == 1,
                msg: "Expected cancel published once");

            Assert.IsOrBecomesTrue(
                       () => Interlocked.Read(ref cancelReturned) == 1,
                       msg: "Expected cancel returned once, actual " + cancelReturned);

        }
        [Fact(Skip = "Connected bus scenarios currently disabled")]
        public void fire_oversubscribed_commands_should_throw_oversubscribed()
        {
            var bus = new CommandBus("local");
            var bus2 = new CommandBus("remote");
            var conn = new BusConnector(bus, bus2);
            long proccessedCmd = 0;
            long gotAck = 0;
            bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand>(
                 cmd =>
                 {
                     Interlocked.Increment(ref proccessedCmd);
                     Task.Delay(1000).Wait();
                     return true;
                 }));
            bus2.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand>(
                  cmd =>
                  {
                      Interlocked.Increment(ref proccessedCmd);
                      Task.Delay(100).Wait();
                      return true;
                  }));
            bus.Subscribe(
                        new AdHocHandler<AckCommand>(cmd => Interlocked.Increment(ref gotAck)));

            Assert.Throws<CommandOversubscribedException>(() =>
                        bus.Fire(new TestCommands.TestCommand(Guid.NewGuid(), null)));

            Assert.IsOrBecomesTrue(
                        () => Interlocked.Read(ref gotAck) == 2,
                        msg: "Expected command Acked twice, got " + gotAck);

            Assert.IsOrBecomesTrue(
                        () => Interlocked.Read(ref proccessedCmd) <= 1,
                        msg: "Expected command handled once or less, actual " + proccessedCmd);
        }
        [Fact]
        public void tryfire_commands_should_not_call_other_commands()
        {
            var bus = new CommandBus("local");
            long gotCmd = 0;


            bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand>(
                 cmd =>
                 {
                     Interlocked.Increment(ref gotCmd);
                     return true;
                 }));
            bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand2>(
                  cmd =>
                  {
                      Interlocked.Increment(ref gotCmd);
                      return true;
                  }));
            CommandResponse response;

            var passed = bus.TryFire(
                new TestCommands.TestCommand(Guid.NewGuid(), null), out response, TimeSpan.FromMilliseconds(1500));

            Assert.True(passed, "Expected false return");
            Thread.Sleep(100); //let other threads complete
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotCmd) == 1,
                msg: "Expected command received once, got " + gotCmd);
            Assert.IsType(typeof(Success), response);

        }
        [Fact]
        public void unsubscribe_should_remove_handler()
        {
            var bus = new CommandBus("local");
            long proccessedCmd = 0;
            var hndl = new AdHocCommandHandler<TestCommands.TestCommand>(
                 cmd =>
                 {
                     Interlocked.Increment(ref proccessedCmd);
                     return true;
                 });
            bus.Subscribe(hndl);
            bus.Fire(new TestCommands.TestCommand(Guid.NewGuid(), null));
            bus.Unsubscribe(hndl);
            Assert.Throws<CommandNotHandledException>(() =>
                 bus.Fire(new TestCommands.TestCommand(Guid.NewGuid(), null)));
            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref proccessedCmd) == 1,
                msg: "Expected command handled once, got" + proccessedCmd);
        }
        [Fact]
        public void can_resubscribe_handler()
        {
            var bus = new CommandBus("local");
            long proccessedCmd = 0;
            var hndl = new AdHocCommandHandler<TestCommands.TestCommand>(
                 cmd =>
                 {
                     Interlocked.Increment(ref proccessedCmd);
                     return true;
                 });
            bus.Subscribe(hndl);
            Assert.IsOrBecomesTrue(() => bus.HasSubscriberFor<TestCommands.TestCommand>());

            bus.Fire(new TestCommands.TestCommand(Guid.NewGuid(), null));
            bus.Unsubscribe(hndl);
            Assert.IsOrBecomesFalse(() => bus.HasSubscriberFor<TestCommands.TestCommand>());

            Assert.Throws<CommandNotHandledException>(
                () => bus.Fire(new TestCommands.TestCommand(Guid.NewGuid(), null)));
            bus.Subscribe(hndl);
            Assert.IsOrBecomesTrue(() => bus.HasSubscriberFor<TestCommands.TestCommand>());

            bus.Fire(new TestCommands.TestCommand(Guid.NewGuid(), null));
            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref proccessedCmd) == 2,
                msg: "Expected command handled twice, got" + proccessedCmd);
        }
        [Fact]
        public void unsubscribe_should_not_remove_other_handlers()
        {
            var bus = new CommandBus("local");
            long proccessedCmd = 0;
            var hndl = new AdHocCommandHandler<TestCommands.TestCommand>(
                 cmd =>
                 {
                     Interlocked.Increment(ref proccessedCmd);
                     return true;
                 });
            bus.Subscribe(hndl);
            bus.Subscribe(new AdHocCommandHandler<TestCommands.TestCommand2>(
                 cmd =>
                 {
                     Interlocked.Increment(ref proccessedCmd);
                     return true;
                 }));
            bus.Fire(new TestCommands.TestCommand(Guid.NewGuid(), null));
            bus.Unsubscribe(hndl);
            Assert.Throws<CommandNotHandledException>(
                () => bus.Fire(new TestCommands.TestCommand(Guid.NewGuid(), null)));

            bus.Fire(new TestCommands.TestCommand2(Guid.NewGuid(), null));
            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref proccessedCmd) == 2,
                msg: "Expected command handled twice, got" + proccessedCmd);
        }
    }
}
