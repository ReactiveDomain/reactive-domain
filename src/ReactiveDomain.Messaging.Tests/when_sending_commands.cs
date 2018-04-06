using System;
using System.Threading;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests
{
    public class TestCommandBusFixture :
        IHandleCommand<TestCommands.Command1>,
        IHandleCommand<TestCommands.Command2>,
        IHandle<AckCommand>,
        IHandle<Message>,
        IHandle<CommandResponse>,
        IHandleCommand<TestCommands.Fail>,
        IHandleCommand<TestCommands.Throw>,
        IHandleCommand<TestCommands.WrapException>,
        IHandleCommand<TestCommands.TypedResponse>,
        IHandleCommand<TestCommands.ChainedCaller>,
        IHandleCommand<TestCommands.RemoteHandled>,
        IHandleCommand<TestCommands.LongRunning>,
        IHandleCommand<TestCommands.Command3>
    {
        public readonly IDispatcher Bus;
        public readonly IDispatcher RemoteBus;

        public long GotChainedCaller;
        public long GotTestCommand1;
        public long GotTestCommand2;
        public long GotRemoteHandled;
        public long GotLongRunning;
        public long GotTestFailCommand;
        public long GotTestThrowCommand;
        public long GotTestWrapCommand;
        public long GotTypedResponse;
        public long GotAck;
        public long GotMessage;
        public long GotCommandResponse;
        public long GotFail;
        public long GotSuccess;
        public long GotCanceled;
        public long ResponseData;
        public long CancelLongRunning;
        public long GotTestCommand3;

        public readonly TimeSpan StandardTimeout;

        public TestCommandBusFixture()
        {
            StandardTimeout = TimeSpan.FromSeconds(0.2);
            Bus = new Dispatcher(nameof(TestCommandBusFixture), 3, false, StandardTimeout, StandardTimeout);
            RemoteBus = new Dispatcher(nameof(TestCommandBusFixture), 3, false, StandardTimeout, StandardTimeout);
            //todo: fix connector
            //var conn = new BusConnector(Bus, RemoteBus);

            Bus.Subscribe<TestCommands.Command1>(this);
            Bus.Subscribe<TestCommands.Command2>(this);
            Bus.Subscribe<AckCommand>(this);
            Bus.Subscribe<Message>(this);
            Bus.Subscribe<CommandResponse>(this);
            Bus.Subscribe<TestCommands.Fail>(this);
            Bus.Subscribe<TestCommands.Throw>(this);
            Bus.Subscribe<TestCommands.WrapException>(this);
            Bus.Subscribe<TestCommands.TypedResponse>(this);
            Bus.Subscribe<TestCommands.ChainedCaller>(this);
            RemoteBus.Subscribe<TestCommands.RemoteHandled>(this);
            Bus.Subscribe<TestCommands.LongRunning>(this);
            //Deliberately not subscribed 
            //Bus.Subscribe<TestCommands.Command3>(this);

            Warmup();
        }

        private void Warmup() {
            //get all the threads and queues running
            var id = CorrelationId.NewId();
            for (int i = 0; i < 100; i++) {
                Bus.TryFire(new TestCommands.Command1(id, SourceId.NullSourceId()));
            }

            SpinWait.SpinUntil(() => Bus.Idle);
            ClearCounters();
        }

        public void ClearCounters()
        {
            Interlocked.Exchange(ref GotChainedCaller, 0);
            Interlocked.Exchange(ref GotTestCommand1, 0);
            Interlocked.Exchange(ref GotTestCommand2, 0);
            Interlocked.Exchange(ref GotRemoteHandled, 0);
            Interlocked.Exchange(ref GotLongRunning, 0);
            Interlocked.Exchange(ref GotTestFailCommand, 0);
            Interlocked.Exchange(ref GotTestThrowCommand, 0);
            Interlocked.Exchange(ref GotTestWrapCommand, 0);
            Interlocked.Exchange(ref GotTypedResponse, 0);
            Interlocked.Exchange(ref GotAck, 0);
            Interlocked.Exchange(ref GotMessage, 0);
            Interlocked.Exchange(ref GotCommandResponse, 0);
            Interlocked.Exchange(ref GotFail, 0);
            Interlocked.Exchange(ref GotSuccess, 0);
            Interlocked.Exchange(ref GotCanceled, 0);
            Interlocked.Exchange(ref ResponseData, 0);
            Interlocked.Exchange(ref CancelLongRunning, 0);
            Interlocked.Exchange(ref GotTestCommand3, 0);
        }

        public CommandResponse Handle(TestCommands.ChainedCaller command)
        {
            Interlocked.Increment(ref GotChainedCaller);
            Bus.Fire(new TestCommands.Command1(command.CorrelationId, new SourceId(command)));
            return command.Succeed();
        }
        public CommandResponse Handle(TestCommands.Command1 command)
        {
            Interlocked.Increment(ref GotTestCommand1);
            return command.Succeed();
        }
        public CommandResponse Handle(TestCommands.Command2 command)
        {
            Interlocked.Increment(ref GotTestCommand2);
            return command.Succeed();
        }
        public CommandResponse Handle(TestCommands.Command3 command)
        {
            Interlocked.Increment(ref GotTestCommand3);
            return command.Succeed();
        }
        public CommandResponse Handle(TestCommands.RemoteHandled command)
        {
            Interlocked.Increment(ref GotRemoteHandled);
            return command.Succeed();
        }
        public CommandResponse Handle(TestCommands.LongRunning command)
        {
            Interlocked.Increment(ref GotLongRunning);
            Interlocked.Exchange(ref CancelLongRunning, 0);
            //wait too long
            var timespan = StandardTimeout + TimeSpan.FromSeconds(3);
            SpinWait.SpinUntil(
                () => Interlocked.Read(
                          ref CancelLongRunning) == 1,
                          timespan);
            return command.Succeed();
        }
        public CommandResponse Handle(TestCommands.Fail command)
        {
            Interlocked.Increment(ref GotTestFailCommand);
            return command.Fail();
        }
        public CommandResponse Handle(TestCommands.Throw command)
        {
            Interlocked.Increment(ref GotTestThrowCommand);
            throw new TestException();
        }
        public CommandResponse Handle(TestCommands.WrapException command)
        {
            Interlocked.Increment(ref GotTestWrapCommand);
            try
            {
                throw new TestException();
            }
            catch (Exception e)
            {
                return command.Fail(e);
            }
        }
        public CommandResponse Handle(TestCommands.TypedResponse command)
        {
            Interlocked.Increment(ref GotTypedResponse);
            var data = 42;
            if (command.FailCommand)
                return command.Fail(new TestException(), data);

            return command.Succeed(data);
        }
        public void Handle(AckCommand command)
        {
            Interlocked.Increment(ref GotAck);
        }
        public void Handle(Message command)
        {
            Interlocked.Increment(ref GotMessage);
        }
        public void Handle(CommandResponse command)
        {
            Interlocked.Increment(ref GotCommandResponse);
            switch (command)
            {
                case Success _:
                    Interlocked.Increment(ref GotSuccess);
                    if (command is TestCommands.TestResponse response)
                        Interlocked.Exchange(ref ResponseData, response.Data);
                    break;
                case Fail _:
                    Interlocked.Increment(ref GotFail);
                    if (command is Canceled)
                        Interlocked.Increment(ref GotCanceled);
                    if (command is TestCommands.FailedResponse failResponse)
                        Interlocked.Exchange(ref ResponseData, failResponse.Data);
                    break;
            }
        }

        public class TestException : Exception { }
    }

    // ReSharper disable once InconsistentNaming
    public class when_sending_commands :
        IClassFixture<TestCommandBusFixture>

    {
        private readonly TestCommandBusFixture _fixture;

        public when_sending_commands(TestCommandBusFixture fixture)
        {
            _fixture = fixture;
        }
        [Fact]
        public void publish_publishes_command_as_message()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            _fixture.ClearCounters();
            _fixture.Bus.Publish(new TestCommands.Command1(CorrelationId.NewId(), SourceId.NullSourceId()));
            SpinWait.SpinUntil(() => Interlocked.Read(ref _fixture.GotTestCommand1) == 1, 250);
        }
        [Fact]
        public void fire_publishes_command_as_message()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            _fixture.ClearCounters();
            _fixture.Bus.Fire(new TestCommands.Command1(CorrelationId.NewId(), SourceId.NullSourceId()));
            SpinWait.SpinUntil(() => Interlocked.Read(ref _fixture.GotTestCommand1) == 1, 250);
        }
        [Fact]
        public void command_handler_acks_command_message()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            _fixture.ClearCounters();
            _fixture.Bus.Fire(new TestCommands.Command1(CorrelationId.NewId(), SourceId.NullSourceId()));
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestCommand1) == 1);
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotAck) == 1);
        }
        [Fact]
        public void command_handler_responds_to_command_message()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            _fixture.ClearCounters();

            _fixture.Bus.Fire(new TestCommands.Command1(CorrelationId.NewId(), SourceId.NullSourceId()));

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotMessage) == 3, null, "Expected 3 Messages");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotAck) == 1, null, "Expected Ack");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestCommand1) == 1, null, "Expected Command was handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotCommandResponse) == 1, null, "Expected Response");
        }
        [Fact]
        public void fire_passing_command_should_pass()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            _fixture.ClearCounters();

            _fixture.Bus.Fire(new TestCommands.Command1(CorrelationId.NewId(), SourceId.NullSourceId()));

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestCommand1) == 1, null, "Expected Command was handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotCommandResponse) == 1, null, "Expected Response");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotSuccess) == 1, null, "Expected Success");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 0, null, "Unexpected fail received.");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotCanceled) == 0, null, "Unexpected Cancel received.");
        }
        [Fact]
        public void fire_failing_command_should_fail()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            _fixture.ClearCounters();

            Assert.Throws<CommandException>(() =>
            _fixture.Bus.Fire(new TestCommands.Fail(CorrelationId.NewId(), SourceId.NullSourceId())));

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestFailCommand) == 1, null, "Expected Command was handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotCommandResponse) == 1, null, "Expected Response");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotSuccess) == 0, null, "Unexpected Success");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 1, null, "Expected fail received.");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotCanceled) == 0, null, "Unexpected Cancel received.");
        }
        [Fact]
        public void try_fire_passing_command_should_pass()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            _fixture.ClearCounters();

            _fixture.Bus.TryFire(new TestCommands.Command1(CorrelationId.NewId(), SourceId.NullSourceId()), out var result);

            Assert.True(result is Success, "Result not success");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestCommand1) == 1, null, "Expected Command was handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotCommandResponse) == 1, null, "Expected Response");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotSuccess) == 1, null, "Expected Success");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 0, null, "Unexpected fail received.");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotCanceled) == 0, null, "Unexpected Cancel received.");
        }
        [Fact]
        public void try_fire_failing_command_should_fail()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            _fixture.ClearCounters();

            _fixture.Bus.TryFire(new TestCommands.Fail(CorrelationId.NewId(), SourceId.NullSourceId()), out var result);

            Assert.True(result is Fail, "Result not fail");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestFailCommand) == 1, null, "Expected Command was handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotCommandResponse) == 1, null, "Expected Response");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotSuccess) == 0, null, "Unexpected Success");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 1, null, "Expected fail received.");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotCanceled) == 0, null, "Unexpected Cancel received.");
        }

        [Fact]
        public void handlers_that_wrap_exceptions_rethrow_on_fire()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            _fixture.ClearCounters();

            Assert.CommandThrows<TestCommandBusFixture.TestException>(() =>
                            _fixture.Bus.Fire(new TestCommands.WrapException(CorrelationId.NewId(), SourceId.NullSourceId())));

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestWrapCommand) == 1, null, "Expected Command was handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotAck) == 1, null, "Expected Ack received.");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 1, null, "Expected fail received.");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotMessage) == 3, null, "Unexpected Number of messages received.");
        }

        [Fact]
        public void handlers_that_throw_exceptions_rethrow_on_fire()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            _fixture.ClearCounters();

            Assert.CommandThrows<TestCommandBusFixture.TestException>(() =>
                _fixture.Bus.Fire(new TestCommands.Throw(CorrelationId.NewId(), SourceId.NullSourceId())));

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestThrowCommand) == 1, null, "Expected Command was handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotAck) == 1, null, "Expected Ack received.");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 1, null, "Expected fail received.");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotMessage) == 3, null, "Unexpected Number of messages received.");

        }

        [Fact]
        public void handlers_that_wrap_exceptions_return_on_tryfire()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            _fixture.ClearCounters();

            Assert.False(_fixture.Bus.TryFire(new TestCommands.WrapException(CorrelationId.NewId(), SourceId.NullSourceId()), out var response));

            Assert.IsType<Fail>(response);
            var fail = (Fail)response;
            Assert.IsType<TestCommandBusFixture.TestException>(fail.Exception);

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestWrapCommand) == 1, null, "Expected Command was handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotAck) == 1, null, "Expected Ack received.");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 1, null, "Expected fail received.");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotMessage) == 3, null, $"Unexpected Number of messages received {Interlocked.Read(ref _fixture.GotMessage)}.");
        }
        [Fact]
        public void handlers_that_throw_exceptions_return_on_tryfire()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            _fixture.ClearCounters();

            Assert.False(_fixture.Bus.TryFire(new TestCommands.Throw(CorrelationId.NewId(), SourceId.NullSourceId()), out var response));

            Assert.IsType<Fail>(response);
            var fail = (Fail)response;
            Assert.IsType<TestCommandBusFixture.TestException>(fail.Exception);

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestThrowCommand) == 1, null, "Expected Command was handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotAck) == 1, null, "Expected Ack received.");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 1, null, "Expected fail received.");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotMessage) == 3, null, "Unexpected Number of messages received.");
        }

        [Fact]
        public void typed_fire_passing_command_should_pass()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            const int data = 42;
            _fixture.ClearCounters();

            _fixture.Bus.Fire(new TestCommands.TypedResponse(false, CorrelationId.NewId(), SourceId.NullSourceId()));

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotSuccess) == 1, null, "Expected Success");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.ResponseData) == data);
        }
        [Fact]
        public void typed_tryfire_passing_command_should_pass()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            const int data = 42;
            _fixture.ClearCounters();

            Assert.True(_fixture.Bus.TryFire(new TestCommands.TypedResponse(false, CorrelationId.NewId(), SourceId.NullSourceId()), out var response), "Expected tryfire to return true");

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotSuccess) == 1, null, "Expected Success");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.ResponseData) == data);
            Assert.IsType(typeof(TestCommands.TestResponse), response);
            Assert.Equal(data, ((TestCommands.TestResponse)response).Data);
        }

        [Fact]
        public void typed_failing_fire_command_should_fail()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            const int data = 42;
            _fixture.ClearCounters();

            Assert.CommandThrows<TestCommandBusFixture.TestException>(() =>
                _fixture.Bus.Fire(new TestCommands.TypedResponse(true, CorrelationId.NewId(), SourceId.NullSourceId())));

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 1, null, "Expected Failure");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.ResponseData) == data);
        }

        [Fact]
        public void typed_failing_tryfire_command_should_fail()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            const int data = 42;
            _fixture.ClearCounters();

            Assert.False(_fixture.Bus.TryFire(new TestCommands.TypedResponse(true, CorrelationId.NewId(), SourceId.NullSourceId()), out var response), "Expected tryfire to return false");

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 1, null, "Expected Failure");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.ResponseData) == data);
            Assert.IsType(typeof(TestCommands.FailedResponse), response);
            Assert.Equal(data, ((TestCommands.FailedResponse)response).Data);
        }

        [Fact]
        public void multiple_commands_can_register()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            _fixture.ClearCounters();

            _fixture.Bus.Fire(new TestCommands.Command1(CorrelationId.NewId(), SourceId.NullSourceId()));
            _fixture.Bus.Fire(new TestCommands.Command2(CorrelationId.NewId(), SourceId.NullSourceId()));

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestCommand1) == 1, msg: "Expected Cmd1 handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestCommand2) == 1, msg: "Expected Cmd2 handled");
        }

        [Fact]
        public void cannot_subscribe_twice_on_same_bus()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            _fixture.ClearCounters();
            Assert.Throws<ExistingHandlerException>(
                () => _fixture.Bus.Subscribe(new AdHocCommandHandler<TestCommands.Command1>(cmd => true)));
        }

        [Fact]
        public void chained_commands_should_not_deadlock()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            _fixture.ClearCounters();
            _fixture.Bus.Fire(new TestCommands.ChainedCaller(CorrelationId.NewId(), SourceId.NullSourceId()));

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestCommand1) == 1, msg: "Expected Command1 to be handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotChainedCaller) == 1, msg: "Expected chained Caller to be handled");
        }
        [Fact]
        public void unsubscribed_commands_should_throw_ack_timeout()
        {
            Assert.Throws<CommandNotHandledException>(() =>
                 _fixture.Bus.Fire(new TestCommands.Unhandled(CorrelationId.NewId(), SourceId.NullSourceId())));
        }

        [Fact]
        public void try_fire_unsubscribed_commands_should_return_throw_commandNotHandledException()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            _fixture.ClearCounters();
            var result = _fixture.Bus.TryFire(new TestCommands.Unhandled(CorrelationId.NewId(), SourceId.NullSourceId()), out var response);

            Assert.False(result, "Expected false return");
            Assert.IsType(typeof(Fail), response);
            Assert.IsType(typeof(CommandNotHandledException), ((Fail)response).Exception);
        }

        [Fact]
        public void slow_commands_should_return_timeout()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            _fixture.ClearCounters();

            Assert.Throws<CommandTimedOutException>(() =>
                _fixture.Bus.Fire(new TestCommands.LongRunning(CorrelationId.NewId(), SourceId.NullSourceId())));
            //n.b. prefer using token cancel commands for canceling, we just need to avoid side effects here while testing basic commands
            Interlocked.Increment(ref _fixture.CancelLongRunning);
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotLongRunning) == 1, msg: "Expected Long Running to be handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotCanceled) == 1, msg: "Expected Long Running to be canceled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotSuccess) == 1, msg: "Expected Long Running to be completed");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotCommandResponse) == 2, msg: "Expected Long Running to be completed twice");
        }
        [Fact]
        public void slow_commands_can_override_timeout()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            _fixture.ClearCounters();

            _fixture.Bus.Fire(new TestCommands.LongRunning(CorrelationId.NewId(), SourceId.NullSourceId()), responseTimeout: TimeSpan.FromSeconds(5));

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotLongRunning) == 1, msg: "Expected Long Running to be handled");
        }
        [Fact(Skip = "Distributed commands are currently disabled")]
        public void passing_commands_on_connected_buses_should_pass()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            _fixture.ClearCounters();

            _fixture.Bus.Fire(new TestCommands.RemoteHandled(CorrelationId.NewId(), SourceId.NullSourceId()));

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotRemoteHandled) == 1, msg: "Expected RemoteHandled to be handled");
        }
        [Fact(Skip = "Connected bus scenarios currently disabled")]
        public void fire_oversubscribed_commands_should_throw_oversubscribed()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            _fixture.ClearCounters();
            Assert.True(false);
            //todo: rewrite this to use the new fixture once the bus connector is fixed
            //var bus2 = new CommandBus("remote");
            //// ReSharper disable once UnusedVariable
            //var conn = new BusConnector(_bus, bus2);
            //long processedCmd = 0;
            //long gotAck = 0;
            //_bus.Subscribe(new AdHocCommandHandler<TestCommands.Command1>(
            //     cmd =>
            //     {
            //         Interlocked.Increment(ref processedCmd);
            //         Task.Delay(1000).Wait();
            //         return true;
            //     }));
            //bus2.Subscribe(new AdHocCommandHandler<TestCommands.Command1>(
            //      cmd =>
            //      {
            //          Interlocked.Increment(ref processedCmd);
            //          Task.Delay(100).Wait();
            //          return true;
            //      }));
            //_bus.Subscribe(
            //            new AdHocHandler<AckCommand>(cmd => Interlocked.Increment(ref gotAck)));

            //Assert.Throws<CommandOversubscribedException>(() =>
            //            _bus.Fire(new TestCommands.Command1(Guid.NewGuid(), null)));

            //Assert.IsOrBecomesTrue(
            //            () => Interlocked.Read(ref gotAck) == 2,
            //            msg: "Expected command Acked twice, got " + gotAck);

            //Assert.IsOrBecomesTrue(
            //            () => Interlocked.Read(ref processedCmd) <= 1,
            //            msg: "Expected command handled once or less, actual " + processedCmd);
        }

        [Fact]
        public void commands_should_not_call_other_commands()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            _fixture.ClearCounters();

            _fixture.Bus.Fire(new TestCommands.Command1(CorrelationId.NewId(), SourceId.NullSourceId()));
            var passed = _fixture.Bus.TryFire(
                new TestCommands.Command1(CorrelationId.NewId(), SourceId.NullSourceId()), out var response);

            Assert.True(passed, "Expected false return");
            Assert.IsType(typeof(Success), response);
            Assert.True(Interlocked.Read(ref _fixture.GotChainedCaller) == 0);
            Assert.True(Interlocked.Read(ref _fixture.GotTestCommand1) == 2);
            Assert.True(Interlocked.Read(ref _fixture.GotTestCommand2) == 0);
            Assert.True(Interlocked.Read(ref _fixture.GotRemoteHandled) == 0);
            Assert.True(Interlocked.Read(ref _fixture.GotLongRunning) == 0);
            Assert.True(Interlocked.Read(ref _fixture.GotTestFailCommand) == 0);
            Assert.True(Interlocked.Read(ref _fixture.GotTestThrowCommand) == 0);
            Assert.True(Interlocked.Read(ref _fixture.GotTestWrapCommand) == 0);
            Assert.True(Interlocked.Read(ref _fixture.GotTypedResponse) == 0);
            Assert.True(Interlocked.Read(ref _fixture.GotAck) == 2);
            Assert.True(Interlocked.Read(ref _fixture.GotCommandResponse) == 2);
            Assert.True(Interlocked.Read(ref _fixture.GotFail) == 0);
            Assert.True(Interlocked.Read(ref _fixture.GotSuccess) == 2);
            Assert.True(Interlocked.Read(ref _fixture.GotCanceled) == 0);
            Assert.True(Interlocked.Read(ref _fixture.ResponseData) == 0);
            Assert.True(Interlocked.Read(ref _fixture.GotMessage) == 6, $"Unexpected Number of events {Interlocked.Read(ref _fixture.GotMessage)}");
        }

        [Fact]
        public void unsubscribe_should_remove_handler()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            _fixture.ClearCounters();

            Assert.False(_fixture.Bus.HasSubscriberFor<TestCommands.Command3>());
            var subscription = _fixture.Bus.Subscribe<TestCommands.Command3>(_fixture);
            Assert.True(_fixture.Bus.HasSubscriberFor<TestCommands.Command3>());
            _fixture.Bus.Fire(new TestCommands.Command3(CorrelationId.NewId(), SourceId.NullSourceId()));
            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref _fixture.GotTestCommand3) == 1,
                msg: "Expected command handled once, got" + Interlocked.Read(ref _fixture.GotTestCommand3));
            subscription.Dispose();
            Assert.False(_fixture.Bus.HasSubscriberFor<TestCommands.Command3>());
            Assert.Throws<CommandNotHandledException>(() => _fixture.Bus.Fire(new TestCommands.Command3(CorrelationId.NewId(), SourceId.NullSourceId())));

        }

        [Fact]
        public void can_resubscribe_handler()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            _fixture.ClearCounters();
            //no subscription
            Assert.False(_fixture.Bus.HasSubscriberFor<TestCommands.Command3>());
            //Add subscription
            var subscription = _fixture.Bus.Subscribe<TestCommands.Command3>(_fixture);
            Assert.True(_fixture.Bus.HasSubscriberFor<TestCommands.Command3>());
            _fixture.Bus.Fire(new TestCommands.Command3(CorrelationId.NewId(), SourceId.NullSourceId()));
            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref _fixture.GotTestCommand3) == 1,
                msg: "Expected command handled once, got" + Interlocked.Read(ref _fixture.GotTestCommand3));
            //dispose subscription to unsubscribe
            subscription.Dispose();
            Assert.False(_fixture.Bus.HasSubscriberFor<TestCommands.Command3>());
            Assert.Throws<CommandNotHandledException>(() => _fixture.Bus.Fire(new TestCommands.Command3(CorrelationId.NewId(), SourceId.NullSourceId())));
            //resubscribe
            subscription = _fixture.Bus.Subscribe<TestCommands.Command3>(_fixture);
            Assert.True(_fixture.Bus.HasSubscriberFor<TestCommands.Command3>());
            _fixture.Bus.Fire(new TestCommands.Command3(CorrelationId.NewId(), SourceId.NullSourceId()));
            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref _fixture.GotTestCommand3) == 2,
                msg: "Expected command handled twice, got" + Interlocked.Read(ref _fixture.GotTestCommand3));
            //cleanup
            subscription.Dispose();

        }

        [Fact]
        public void unsubscribe_should_not_remove_other_handlers()
        {
            Assert.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            _fixture.ClearCounters();

            Interlocked.Exchange(ref _fixture.GotTestCommand3, 0);
            //no subscription
            Assert.False(_fixture.Bus.HasSubscriberFor<TestCommands.Command3>());
            //Add subscription
            var subscription = _fixture.Bus.Subscribe<TestCommands.Command3>(_fixture);
            Assert.True(_fixture.Bus.HasSubscriberFor<TestCommands.Command3>());
            _fixture.Bus.Fire(new TestCommands.Command3(CorrelationId.NewId(), SourceId.NullSourceId()));
            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref _fixture.GotTestCommand3) == 1,
                msg: "Expected command handled once, got" + Interlocked.Read(ref _fixture.GotTestCommand3));
            //dispose subscription to unsubscribe
            subscription.Dispose();
            Assert.False(_fixture.Bus.HasSubscriberFor<TestCommands.Command3>());
            Assert.Throws<CommandNotHandledException>(() => _fixture.Bus.Fire(new TestCommands.Command3(CorrelationId.NewId(), SourceId.NullSourceId())));

            Assert.True(_fixture.Bus.HasSubscriberFor<TestCommands.Command1>());
            Assert.True(_fixture.Bus.HasSubscriberFor<TestCommands.Command2>());
            Assert.True(_fixture.Bus.HasSubscriberFor<TestCommands.Fail>());
            Assert.True(_fixture.Bus.HasSubscriberFor<TestCommands.Throw>());
            Assert.True(_fixture.Bus.HasSubscriberFor<TestCommands.WrapException>());
            Assert.True(_fixture.Bus.HasSubscriberFor<TestCommands.TypedResponse>());
            Assert.True(_fixture.Bus.HasSubscriberFor<TestCommands.ChainedCaller>());
            Assert.True(_fixture.Bus.HasSubscriberFor<TestCommands.LongRunning>());

        }

    }
}
