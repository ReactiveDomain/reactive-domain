using System;
using System.Threading;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests {
    public class TestCommandBusFixture :
        IHandleCommand<TestCommands.AckedCommand>,
        IHandleCommand<TestCommands.Command1>,
        IHandleCommand<TestCommands.Command2>,
        IHandleCommand<TestCommands.ParentCommand>,
        IHandle<AckCommand>,
        IHandle<Message>,
        IHandle<CommandResponse>,
        IHandleCommand<TestCommands.Fail>,
        IHandleCommand<TestCommands.Throw>,
        IHandleCommand<TestCommands.WrapException>,
        IHandleCommand<TestCommands.TypedResponse>,
        IHandleCommand<TestCommands.ChainedCaller>,
        IHandleCommand<TestCommands.LongRunning>,
        IHandleCommand<TestCommands.Command3>,
        IHandle<TestEvent> {
        public readonly IDispatcher Bus;

        public long GotChainedCaller;
        public long GotAckedCommand;
        public long GotTestCommand1;
        public long GotTestCommand2;
        public long GotParentCommand;
        public long GotLongRunning;
        public long CancelLongRunning;
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
        public long GotTestCommand3;
        public long GotTestEvent;

        public readonly TimeSpan StandardTimeout;

        public TestCommandBusFixture() {
            StandardTimeout = TimeSpan.FromSeconds(0.2);
            Bus = new Dispatcher(nameof(TestCommandBusFixture), 3, false, StandardTimeout, StandardTimeout);
          
            Bus.Subscribe<TestCommands.AckedCommand>(this);
            Bus.Subscribe<TestCommands.Command1>(this);
            Bus.Subscribe<TestCommands.Command2>(this);
            Bus.Subscribe<TestCommands.ParentCommand>(this);
            Bus.Subscribe<AckCommand>(this);
            Bus.Subscribe<Message>(this);
            Bus.Subscribe<CommandResponse>(this);
            Bus.Subscribe<TestCommands.Fail>(this);
            Bus.Subscribe<TestCommands.Throw>(this);
            Bus.Subscribe<TestCommands.WrapException>(this);
            Bus.Subscribe<TestCommands.TypedResponse>(this);
            Bus.Subscribe<TestCommands.ChainedCaller>(this);
            Bus.Subscribe<TestCommands.LongRunning>(this);
            Bus.Subscribe<TestEvent>(this);
            //Deliberately not subscribed 
            //Bus.Subscribe<TestCommands.Command3>(this);
        }

        public void ClearCounters() {
            Interlocked.Exchange(ref GotChainedCaller, 0);
            Interlocked.Exchange(ref GotAckedCommand, 0);
            Interlocked.Exchange(ref GotTestCommand1, 0);
            Interlocked.Exchange(ref GotTestCommand2, 0);
            Interlocked.Exchange(ref GotParentCommand, 0);
            Interlocked.Exchange(ref GotLongRunning, 0);
            Interlocked.Exchange(ref CancelLongRunning, 0);
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
            Interlocked.Exchange(ref GotTestCommand3, 0);
            Interlocked.Exchange(ref GotTestEvent, 0);
        }

        public CommandResponse Handle(TestCommands.ChainedCaller command) {
            Interlocked.Increment(ref GotChainedCaller);
            Bus.Send(MessageBuilder
                        .From(command)
                        .Build(()=> new TestCommands.Command1()));
            return command.Succeed();
        }
        public CommandResponse Handle(TestCommands.AckedCommand command) {
            Interlocked.Increment(ref GotAckedCommand);
            //avoid race condition in test don't complete the command until ack returned
            SpinWait.SpinUntil(() => Interlocked.Read(ref GotAck) == 1);
            return command.Succeed();
        }
        public CommandResponse Handle(TestCommands.Command1 command) {
            Interlocked.Increment(ref GotTestCommand1);
            return command.Succeed();
        }
        public CommandResponse Handle(TestCommands.Command2 command) {
            Interlocked.Increment(ref GotTestCommand2);
            return command.Succeed();
        }
        public CommandResponse Handle(TestCommands.ParentCommand command) {
            Interlocked.Increment(ref GotParentCommand);
            return command.Succeed();
        }
        public CommandResponse Handle(TestCommands.Command3 command) {
            Interlocked.Increment(ref GotTestCommand3);
            return command.Succeed();
        }
        public CommandResponse Handle(TestCommands.LongRunning command) {
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
        public CommandResponse Handle(TestCommands.Fail command) {
            Interlocked.Increment(ref GotTestFailCommand);
            //avoid race condition in test don't complete the command until ack returned
            SpinWait.SpinUntil(() => Interlocked.Read(ref GotAck) == 1);
            return command.Fail();
        }
        public CommandResponse Handle(TestCommands.Throw command) {
            Interlocked.Increment(ref GotTestThrowCommand);
            //avoid race condition in test don't complete the command until ack returned
            SpinWait.SpinUntil(() => Interlocked.Read(ref GotAck) == 1);
            throw new TestException();
        }
        public CommandResponse Handle(TestCommands.WrapException command) {
            Interlocked.Increment(ref GotTestWrapCommand);
            //avoid race condition in test don't complete the command until ack returned
            SpinWait.SpinUntil(() => Interlocked.Read(ref GotAck) == 1);
            try {
                throw new TestException();
            }
            catch (Exception e) {
                return command.Fail(e);
            }
        }
        public CommandResponse Handle(TestCommands.TypedResponse command) {
            Interlocked.Increment(ref GotTypedResponse);
            var data = 42;
            if (command.FailCommand)
                return command.Fail(new TestException(), data);

            return command.Succeed(data);
        }
        public void Handle(AckCommand command) {
            Interlocked.Increment(ref GotAck);
        }
        public void Handle(Message command) {
            Interlocked.Increment(ref GotMessage);
        }
        public void Handle(CommandResponse command) {
            Interlocked.Increment(ref GotCommandResponse);
            switch (command) {
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

        public void Handle(TestEvent message) {
            Interlocked.Increment(ref GotTestEvent);
        }
    }

    // ReSharper disable once InconsistentNaming
    public class when_sending_commands :
        IClassFixture<TestCommandBusFixture> {
        private readonly TestCommandBusFixture _fixture;

        public when_sending_commands(TestCommandBusFixture fixture) {
            _fixture = fixture;
            //clear the pipes :-) to make sure we are starting fresh on the fixture
            //one event for each queuedhandler
            fixture.Bus.Publish(new TestEvent());
            fixture.Bus.Publish(new TestEvent());
            fixture.Bus.Publish(new TestEvent());
            AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestEvent) >= 3);
            AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
            _fixture.ClearCounters();
        }

        [Fact]
        public void publish_publishes_command_as_message() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                _fixture.ClearCounters();
                _fixture.Bus.Publish(new TestCommands.Command1());
                SpinWait.SpinUntil(() => Interlocked.Read(ref _fixture.GotTestCommand1) == 1, 250);
            }
        }

        [Fact]
        public void send_publishes_command_as_message() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                _fixture.ClearCounters();
                _fixture.Bus.Send(new TestCommands.Command1());
                SpinWait.SpinUntil(() => Interlocked.Read(ref _fixture.GotTestCommand1) == 1, 250);
            }
        }

        [Fact]
        public void command_handler_acks_command_message() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                _fixture.ClearCounters();
                _fixture.Bus.Send(new TestCommands.AckedCommand());
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotAckedCommand) == 1);
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotAck) == 1);
            }
        }

        [Fact]
        public void command_handler_responds_to_command_message() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                _fixture.ClearCounters();

                _fixture.Bus.Send(new TestCommands.AckedCommand());

                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotAck) == 1, null, "Expected Ack");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotAckedCommand) == 1, null,
                    "Expected Command was handled");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotCommandResponse) == 1, null,
                    "Expected Response");
            }
        }

        [Fact]
        public void send_passing_command_should_pass() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                _fixture.ClearCounters();

                _fixture.Bus.Send(new TestCommands.Command1());

                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestCommand1) == 1, null,
                    "Expected Command was handled");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotCommandResponse) == 1, null,
                    "Expected Response");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotSuccess) == 1, null, "Expected Success");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 0, null,
                    "Unexpected fail received.");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotCanceled) == 0, null,
                    "Unexpected Cancel received.");
            }
        }
        [Fact]
        public void send_failing_command_should_fail() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                _fixture.ClearCounters();

                Assert.Throws<CommandException>(() =>
                    _fixture.Bus.Send(new TestCommands.Fail()));

                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestFailCommand) == 1, null,
                    "Expected Command was handled");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotCommandResponse) == 1, null,
                    "Expected Response");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotSuccess) == 0, null,
                    "Unexpected Success");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 1, null,
                    "Expected fail received.");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotCanceled) == 0, null,
                    "Unexpected Cancel received.");
            }
        }

        [Fact]
        public void try_send_passing_command_should_pass() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                _fixture.ClearCounters();
                _fixture.Bus.TrySend(new TestCommands.Command1(), out var result);
                Assert.True(result is Success, "Result not success");
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle, 4000);
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestCommand1) == 1, null,
                    "Expected Command was handled");
            }
        }

        [Fact]
        public void try_send_failing_command_should_fail() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                _fixture.ClearCounters();

                _fixture.Bus.TrySend(new TestCommands.Fail(), out var result);

                Assert.True(result is Fail, "Result not fail");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestFailCommand) == 1, null,
                    "Expected Command was handled");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotCommandResponse) == 1, null,
                    "Expected Response");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotSuccess) == 0, null,
                    "Unexpected Success");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 1, null,
                    "Expected fail received.");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotCanceled) == 0, null,
                    "Unexpected Cancel received.");
            }
        }
        [Fact]
        public void command_handlers_only_handle_exact_type() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                _fixture.ClearCounters();

                _fixture.Bus.Send(new TestCommands.ParentCommand());
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotParentCommand) == 1);

                _fixture.Bus.Publish(new TestCommands.ChildCommand());
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotParentCommand) == 1);

                _fixture.Bus.Publish(new TestCommands.ParentCommand());
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotParentCommand) == 2);

            }
        }
        [Fact]
        public void handlers_that_wrap_exceptions_rethrow_on_send() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                _fixture.ClearCounters();

                AssertEx.CommandThrows<TestCommandBusFixture.TestException>(() =>
                    _fixture.Bus.Send(new TestCommands.WrapException()));

                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestWrapCommand) == 1, null,
                    "Expected Command was handled");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotAck) == 1, null,
                    "Expected Ack received.");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 1, null,
                    "Expected fail received.");
            }

        }

        [Fact]
        public void handlers_that_throw_exceptions_rethrow_on_send() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                _fixture.ClearCounters();

                AssertEx.CommandThrows<TestCommandBusFixture.TestException>(() =>
                    _fixture.Bus.Send(new TestCommands.Throw()));

                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestThrowCommand) == 1, null,
                    "Expected Command was handled");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotAck) == 1, null,
                    "Expected Ack received.");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 1, null,
                    "Expected fail received.");
            }
        }

        [Fact]
        public void handlers_that_wrap_exceptions_return_on_trysend() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                _fixture.ClearCounters();

                Assert.False(_fixture.Bus.TrySend(new TestCommands.WrapException(),
                    out var response));

                Assert.IsType<Fail>(response);
                var fail = (Fail)response;
                Assert.IsType<TestCommandBusFixture.TestException>(fail.Exception);

                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestWrapCommand) == 1, null,
                    "Expected Command was handled");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotAck) == 1, null,
                    "Expected Ack received.");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 1, null,
                    "Expected fail received.");
            }
        }

        [Fact]
        public void handlers_that_throw_exceptions_return_on_trysend() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                _fixture.ClearCounters();

                Assert.False(
                    _fixture.Bus.TrySend(new TestCommands.Throw(), out var response));

                Assert.IsType<Fail>(response);
                var fail = (Fail)response;
                Assert.IsType<TestCommandBusFixture.TestException>(fail.Exception);

                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestThrowCommand) == 1, null,
                    "Expected Command was handled");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotAck) == 1, null,
                    "Expected Ack received.");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 1, null,
                    "Expected fail received.");
            }
        }

        [Fact]
        public void typed_send_passing_command_should_pass() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                const int data = 42;
                _fixture.ClearCounters();

                _fixture.Bus.Send(new TestCommands.TypedResponse(false));

                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotSuccess) == 1, null, "Expected Success");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.ResponseData) == data);
            }
        }

        [Fact]
        public void typed_trysend_passing_command_should_pass() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                const int data = 42;
                _fixture.ClearCounters();

                Assert.True(
                    _fixture.Bus.TrySend(new TestCommands.TypedResponse(false),
                        out var response), "Expected trysend to return true");

                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotSuccess) == 1, null, "Expected Success");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.ResponseData) == data);
                Assert.IsType<TestCommands.TestResponse>(response);
                Assert.Equal(data, ((TestCommands.TestResponse)response).Data);
            }
        }

        [Fact]
        public void typed_failing_send_command_should_fail() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                const int data = 42;
                _fixture.ClearCounters();

                AssertEx.CommandThrows<TestCommandBusFixture.TestException>(() =>
                    _fixture.Bus.Send(new TestCommands.TypedResponse(true)));

                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 1, null, "Expected Failure");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.ResponseData) == data);
            }
        }

        [Fact]
        public void typed_failing_trysend_command_should_fail() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                const int data = 42;
                _fixture.ClearCounters();

                Assert.False(
                    _fixture.Bus.TrySend(new TestCommands.TypedResponse(true),
                        out var response), "Expected trysend to return false");

                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 1, null, "Expected Failure");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.ResponseData) == data);
                Assert.IsType<TestCommands.FailedResponse>(response);
                Assert.Equal(data, ((TestCommands.FailedResponse)response).Data);
            }
        }

        [Fact]
        public void multiple_commands_can_register() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                _fixture.ClearCounters();

                _fixture.Bus.Send(new TestCommands.Command1());
                _fixture.Bus.Send(new TestCommands.Command2());

                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestCommand1) == 1,
                    msg: "Expected Cmd1 handled");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestCommand2) == 1,
                    msg: "Expected Cmd2 handled");
            }
        }

        [Fact]
        public void cannot_subscribe_twice_on_same_bus() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                _fixture.ClearCounters();
                Assert.Throws<ExistingHandlerException>(
                    () => _fixture.Bus.Subscribe(new AdHocCommandHandler<TestCommands.Command1>(cmd => true)));
            }
        }

        [Fact]
        public void chained_commands_should_not_deadlock() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                _fixture.ClearCounters();
                _fixture.Bus.Send(new TestCommands.ChainedCaller());

                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestCommand1) == 1,
                    msg: "Expected Command1 to be handled");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotChainedCaller) == 1,
                    msg: "Expected chained Caller to be handled");
            }
        }

        [Fact]
        public void unsubscribed_commands_should_throw_ack_timeout() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                Assert.Throws<CommandNotHandledException>(() =>
                    _fixture.Bus.Send(new TestCommands.Unhandled()));
            }
        }

        [Fact]
        public void try_send_unsubscribed_commands_should_return_throw_commandNotHandledException() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                _fixture.ClearCounters();
                var result = _fixture.Bus.TrySend(new TestCommands.Unhandled(),
                    out var response);

                Assert.False(result, "Expected false return");
                Assert.IsType<Fail>(response);
                Assert.IsType<CommandNotHandledException>(((Fail)response).Exception);
            }
        }

        [Fact]
        public void slow_commands_should_return_timeout() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                _fixture.ClearCounters();

                Assert.Throws<CommandTimedOutException>(() =>
                    _fixture.Bus.Send(new TestCommands.LongRunning()));
                //n.b. prefer using token cancel commands for canceling, we just need to avoid side effects here while testing basic commands
                Interlocked.Increment(ref _fixture.CancelLongRunning);
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotLongRunning) == 1,
                    msg: "Expected Long Running to be handled");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotCanceled) == 1,
                    msg: "Expected Long Running to be canceled");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotSuccess) == 1,
                    msg: "Expected Long Running to be completed");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotCommandResponse) == 2,
                    msg: "Expected Long Running to be completed twice");
            }
        }

        [Fact]
        public void slow_commands_can_override_timeout() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                _fixture.ClearCounters();

                _fixture.Bus.Send(new TestCommands.LongRunning(),
                    responseTimeout: TimeSpan.FromSeconds(5));

                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotLongRunning) == 1,
                    msg: "Expected Long Running to be handled");
            }
        }

        [Fact]
        public void commands_should_not_call_other_commands() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                _fixture.ClearCounters();

                _fixture.Bus.Send(new TestCommands.AckedCommand());
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotAckedCommand) == 1, msg: "Command not Handled");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotAck) == 1, msg: "Ack not Handled");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotSuccess) == 1, msg: "Success not Handled");
            }
        }

        [Fact]
        public void unsubscribe_should_remove_handler() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                _fixture.ClearCounters();

                Assert.False(_fixture.Bus.HasSubscriberFor<TestCommands.Command3>());
                var subscription = _fixture.Bus.Subscribe<TestCommands.Command3>(_fixture);
                Assert.True(_fixture.Bus.HasSubscriberFor<TestCommands.Command3>());
                _fixture.Bus.Send(new TestCommands.Command3());
                AssertEx.IsOrBecomesTrue(
                    () => Interlocked.Read(ref _fixture.GotTestCommand3) == 1,
                    msg: "Expected command handled once, got" + Interlocked.Read(ref _fixture.GotTestCommand3));
                subscription.Dispose();
                Assert.False(_fixture.Bus.HasSubscriberFor<TestCommands.Command3>());
                Assert.Throws<CommandNotHandledException>(() =>
                    _fixture.Bus.Send(new TestCommands.Command3()));

            }
        }

        [Fact]
        public void can_resubscribe_handler() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                _fixture.ClearCounters();
                //no subscription
                Assert.False(_fixture.Bus.HasSubscriberFor<TestCommands.Command3>());
                //Add subscription
                var subscription = _fixture.Bus.Subscribe<TestCommands.Command3>(_fixture);
                Assert.True(_fixture.Bus.HasSubscriberFor<TestCommands.Command3>());
                _fixture.Bus.Send(new TestCommands.Command3());
                AssertEx.IsOrBecomesTrue(
                    () => Interlocked.Read(ref _fixture.GotTestCommand3) == 1,
                    msg: "Expected command handled once, got" + Interlocked.Read(ref _fixture.GotTestCommand3));
                //dispose subscription to unsubscribe
                subscription.Dispose();
                Assert.False(_fixture.Bus.HasSubscriberFor<TestCommands.Command3>());
                Assert.Throws<CommandNotHandledException>(() =>
                    _fixture.Bus.Send(new TestCommands.Command3()));
                //resubscribe
                subscription = _fixture.Bus.Subscribe<TestCommands.Command3>(_fixture);
                Assert.True(_fixture.Bus.HasSubscriberFor<TestCommands.Command3>());
                _fixture.Bus.Send(new TestCommands.Command3());
                AssertEx.IsOrBecomesTrue(
                    () => Interlocked.Read(ref _fixture.GotTestCommand3) == 2,
                    msg: "Expected command handled twice, got" + Interlocked.Read(ref _fixture.GotTestCommand3));
                //cleanup
                subscription.Dispose();
            }
        }

        [Fact]
        public void unsubscribe_should_not_remove_other_handlers() {
            lock (_fixture) {
                AssertEx.IsOrBecomesTrue(() => _fixture.Bus.Idle);
                _fixture.ClearCounters();

                Interlocked.Exchange(ref _fixture.GotTestCommand3, 0);
                //no subscription
                Assert.False(_fixture.Bus.HasSubscriberFor<TestCommands.Command3>());
                //Add subscription
                var subscription = _fixture.Bus.Subscribe<TestCommands.Command3>(_fixture);
                Assert.True(_fixture.Bus.HasSubscriberFor<TestCommands.Command3>());
                _fixture.Bus.Send(new TestCommands.Command3());
                AssertEx.IsOrBecomesTrue(
                    () => Interlocked.Read(ref _fixture.GotTestCommand3) == 1,
                    msg: "Expected command handled once, got" + Interlocked.Read(ref _fixture.GotTestCommand3));
                //dispose subscription to unsubscribe
                subscription.Dispose();
                Assert.False(_fixture.Bus.HasSubscriberFor<TestCommands.Command3>());
                Assert.Throws<CommandNotHandledException>(() =>
                    _fixture.Bus.Send(new TestCommands.Command3()));

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
}
