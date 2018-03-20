using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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
        IHandleCommand<TestCommands.RemoteHandled>
    {
        public readonly IGeneralBus Bus;
        public readonly IGeneralBus RemoteBus;
        readonly BusConnector _conn;
        public long GotChainedCaller;
        public long GotTestCommand1;
        public long GotTestCommand2;
        public long GotRemoteHandled;
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


        public TestCommandBusFixture()
        {
            Bus = new CommandBus(nameof(TestCommandBusFixture), false, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
            RemoteBus = new CommandBus(nameof(TestCommandBusFixture), false, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
            //todo: fix connector
            //_conn = new BusConnector(Bus, RemoteBus);

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
        }
        public CommandResponse Handle(TestCommands.ChainedCaller command)
        {
            Interlocked.Increment(ref GotChainedCaller);
            Bus.Fire(new TestCommands.Command1(command.CorrelationId,command.MsgId));
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
        public CommandResponse Handle(TestCommands.RemoteHandled command)
        {
            Interlocked.Increment(ref GotRemoteHandled);
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

        public when_sending_commands(TestCommandBusFixture fixture){
            _fixture = fixture;
        }
        [Fact]
        public void publish_publishes_command_as_message()
        {
            Interlocked.Exchange(ref _fixture.GotTestCommand1, 0);
            _fixture.Bus.Publish(new TestCommands.Command1(Guid.NewGuid(), null));
            SpinWait.SpinUntil(() => Interlocked.Read(ref _fixture.GotTestCommand1) == 1, 250);
        }
        [Fact]
        public void fire_publishes_command_as_message()
        {
            Interlocked.Exchange(ref _fixture.GotTestCommand1, 0);
            _fixture.Bus.Fire(new TestCommands.Command1(Guid.NewGuid(), null));
            SpinWait.SpinUntil(() => Interlocked.Read(ref _fixture.GotTestCommand1) == 1, 250);
        }
        [Fact]
        public void command_handler_acks_command_message()
        {
            Interlocked.Exchange(ref _fixture.GotTestCommand1, 0);
            Interlocked.Exchange(ref _fixture.GotAck, 0);
            _fixture.Bus.Fire(new TestCommands.Command1(Guid.NewGuid(), null));
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestCommand1) == 1);
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotAck) == 1);
        }
        [Fact]
        public void command_handler_responds_to_command_message()
        {
            Interlocked.Exchange(ref _fixture.GotTestCommand1, 0);
            Interlocked.Exchange(ref _fixture.GotAck, 0);
            Interlocked.Exchange(ref _fixture.GotCommandResponse, 0);
            Interlocked.Exchange(ref _fixture.GotMessage, 0);

            _fixture.Bus.Fire(new TestCommands.Command1(Guid.NewGuid(), null));

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotMessage) == 3, null, "Expected 3 Messages");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotAck) == 1, null, "Expected Ack");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestCommand1) == 1, null, "Expected Command was handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotCommandResponse) == 1, null, "Expected Response");
        }
        [Fact]
        public void fire_passing_command_should_pass()
        {
            Interlocked.Exchange(ref _fixture.GotTestCommand1, 0);
            Interlocked.Exchange(ref _fixture.GotFail, 0);
            Interlocked.Exchange(ref _fixture.GotSuccess, 0);
            Interlocked.Exchange(ref _fixture.GotCanceled, 0);
            Interlocked.Exchange(ref _fixture.GotCommandResponse, 0);

            _fixture.Bus.Fire(new TestCommands.Command1(Guid.NewGuid(), null));

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestCommand1) == 1, null, "Expected Command was handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotCommandResponse) == 1, null, "Expected Response");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotSuccess) == 1, null, "Expected Success");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 0, null, "Unexpected fail received.");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotCanceled) == 0, null, "Unexpected Cancel received.");
        }
        [Fact]
        public void fire_failing_command_should_fail()
        {
            Interlocked.Exchange(ref _fixture.GotTestFailCommand, 0);
            Interlocked.Exchange(ref _fixture.GotFail, 0);
            Interlocked.Exchange(ref _fixture.GotSuccess, 0);
            Interlocked.Exchange(ref _fixture.GotCanceled, 0);
            Interlocked.Exchange(ref _fixture.GotCommandResponse, 0);

            Assert.Throws<CommandException>(() =>
            _fixture.Bus.Fire(new TestCommands.Fail(Guid.NewGuid(), null)));

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestFailCommand) == 1, null, "Expected Command was handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotCommandResponse) == 1, null, "Expected Response");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotSuccess) == 0, null, "Unexpected Success");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 1, null, "Expected fail received.");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotCanceled) == 0, null, "Unexpected Cancel received.");
        }
        [Fact]
        public void try_fire_passing_command_should_pass()
        {
            Interlocked.Exchange(ref _fixture.GotTestCommand1, 0);
            Interlocked.Exchange(ref _fixture.GotFail, 0);
            Interlocked.Exchange(ref _fixture.GotSuccess, 0);
            Interlocked.Exchange(ref _fixture.GotCanceled, 0);
            Interlocked.Exchange(ref _fixture.GotCommandResponse, 0);

            _fixture.Bus.TryFire(new TestCommands.Command1(Guid.NewGuid(), null), out var result);

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
            Interlocked.Exchange(ref _fixture.GotTestFailCommand, 0);
            Interlocked.Exchange(ref _fixture.GotFail, 0);
            Interlocked.Exchange(ref _fixture.GotSuccess, 0);
            Interlocked.Exchange(ref _fixture.GotCanceled, 0);
            Interlocked.Exchange(ref _fixture.GotCommandResponse, 0);

            _fixture.Bus.TryFire(new TestCommands.Fail(Guid.NewGuid(), null), out var result);

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

            Interlocked.Exchange(ref _fixture.GotTestWrapCommand, 0);
            Interlocked.Exchange(ref _fixture.GotAck, 0);
            Interlocked.Exchange(ref _fixture.GotFail, 0);
            Interlocked.Exchange(ref _fixture.GotMessage, 0);

            Assert.CommandThrows<TestCommandBusFixture.TestException>(() =>
                            _fixture.Bus.Fire(new TestCommands.WrapException(Guid.NewGuid(), null)));

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestWrapCommand) == 1, null, "Expected Command was handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotAck) == 1, null, "Expected Ack received.");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 1, null, "Expected fail received.");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotMessage) == 3, null, "Unexpected Number of messages received.");
        }

        [Fact]
        public void handlers_that_throw_exceptions_rethrow_on_fire()
        {
            Interlocked.Exchange(ref _fixture.GotTestThrowCommand, 0);
            Interlocked.Exchange(ref _fixture.GotAck, 0);
            Interlocked.Exchange(ref _fixture.GotFail, 0);
            Interlocked.Exchange(ref _fixture.GotMessage, 0);

            Assert.CommandThrows<TestCommandBusFixture.TestException>(() =>
                _fixture.Bus.Fire(new TestCommands.Throw(Guid.NewGuid(), null)));

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestThrowCommand) == 1, null, "Expected Command was handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotAck) == 1, null, "Expected Ack received.");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 1, null, "Expected fail received.");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotMessage) == 3, null, "Unexpected Number of messages received.");

        }

        [Fact]
        public void handlers_that_wrap_exceptions_return_on_tryfire()
        {
            Interlocked.Exchange(ref _fixture.GotTestWrapCommand, 0);
            Interlocked.Exchange(ref _fixture.GotAck, 0);
            Interlocked.Exchange(ref _fixture.GotFail, 0);
            Interlocked.Exchange(ref _fixture.GotMessage, 0);

            Assert.False(_fixture.Bus.TryFire(new TestCommands.WrapException(Guid.NewGuid(), null), out var response));

            Assert.IsType<Fail>(response);
            var fail = (Fail)response;
            Assert.IsType<TestCommandBusFixture.TestException>(fail.Exception);

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestWrapCommand) == 1, null, "Expected Command was handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotAck) == 1, null, "Expected Ack received.");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 1, null, "Expected fail received.");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotMessage) == 3, null, "Unexpected Number of messages received.");
        }
        [Fact]
        public void handlers_that_throw_exceptions_return_on_tryfire()
        {


            Interlocked.Exchange(ref _fixture.GotTestThrowCommand, 0);
            Interlocked.Exchange(ref _fixture.GotAck, 0);
            Interlocked.Exchange(ref _fixture.GotFail, 0);
            Interlocked.Exchange(ref _fixture.GotMessage, 0);

            Assert.False(_fixture.Bus.TryFire(new TestCommands.Throw(Guid.NewGuid(), null), out var response));

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
            const int data = 42;
            Interlocked.Exchange(ref _fixture.ResponseData, 0);
            Interlocked.Exchange(ref _fixture.GotSuccess, 0);

            _fixture.Bus.Fire(new TestCommands.TypedResponse(false, Guid.NewGuid(), null));

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotSuccess) == 1, null, "Expected Success");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.ResponseData) == data);
        }
        [Fact]
        public void typed_tryfire_passing_command_should_pass()
        {
            const int data = 42;
            Interlocked.Exchange(ref _fixture.ResponseData, 0);
            Interlocked.Exchange(ref _fixture.GotSuccess, 0);

            Assert.True(_fixture.Bus.TryFire(new TestCommands.TypedResponse(false, Guid.NewGuid(), null), out var response), "Expected tryfire to return true");

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotSuccess) == 1, null, "Expected Success");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.ResponseData) == data);
            Assert.IsType(typeof(TestCommands.TestResponse), response);
            Assert.Equal(data, ((TestCommands.TestResponse)response).Data);
        }

        [Fact]
        public void typed_failing_fire_command_should_fail()
        {
            const int data = 42;
            Interlocked.Exchange(ref _fixture.ResponseData, 0);
            Interlocked.Exchange(ref _fixture.GotFail, 0);
            
            Assert.CommandThrows<TestCommandBusFixture.TestException>(()=>
                _fixture.Bus.Fire(new TestCommands.TypedResponse(true, Guid.NewGuid(), null)));

            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 1, null, "Expected Failure");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.ResponseData) == data);
        }
       
           [Fact]
           public void typed_failing_tryfire_command_should_fail()
           {
               const int data = 42;
               Interlocked.Exchange(ref _fixture.ResponseData, 0);
               Interlocked.Exchange(ref _fixture.GotFail, 0);

               Assert.False(_fixture.Bus.TryFire(new TestCommands.TypedResponse(true, Guid.NewGuid(), null), out var response), "Expected tryfire to return false");

               Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotFail) == 1, null, "Expected Failure");
               Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.ResponseData) == data);
               Assert.IsType(typeof(TestCommands.FailedResponse), response);
               Assert.Equal(data, ((TestCommands.FailedResponse)response).Data);
           }
       
          [Fact]
          public void multiple_commands_can_register()
          {
              Interlocked.Exchange(ref _fixture.GotTestCommand1, 0);
              Interlocked.Exchange(ref _fixture.GotTestCommand2, 0);

              _fixture.Bus.Fire(new TestCommands.Command1(Guid.NewGuid(), null));
              _fixture.Bus.Fire(new TestCommands.Command2(Guid.NewGuid(), null));

              Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestCommand1) == 1, msg: "Expected Cmd1 handled");
              Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestCommand2) == 1, msg: "Expected Cmd2 handled");
          }
       
         [Fact]
         public void cannot_subscribe_twice_on_same_bus()
         {
             Assert.Throws<ExistingHandlerException>(
                 () => _fixture.Bus.Subscribe(new AdHocCommandHandler<TestCommands.Command1>(cmd => true)));
         }
       
         [Fact]
         public void chained_commands_should_not_deadlock()
         {
             Interlocked.Exchange(ref _fixture.GotTestCommand1, 0);
             Interlocked.Exchange(ref _fixture.GotChainedCaller, 0);
            _fixture.Bus.Fire(new TestCommands.ChainedCaller(Guid.NewGuid(),null));
       
             Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotTestCommand1) == 1, msg: "Expected Command1 to be handled");
             Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotChainedCaller) == 1, msg: "Expected chained Caller to be handled");
         }
       
        [Fact(Skip = "Distributed commands are currently disabled")]
        public void passing_commands_on_connected_buses_should_pass()
        {
            Interlocked.Exchange(ref _fixture.GotRemoteHandled, 0);
            _fixture.Bus.Fire(new TestCommands.RemoteHandled(Guid.NewGuid(),null));
       
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.GotRemoteHandled) == 1, msg: "Expected RemoteHandled to be handled");
        }
        /*
             [Fact]
             public void unsubscribed_commands_should_throw_ack_timeout()
             {
        
                 Assert.Throws<CommandNotHandledException>(() =>
                      _bus.Fire(new TestCommands.Command1(Guid.NewGuid(), null)));
             }
        
             [Fact]
             public void try_fire_unsubscribed_commands_should_return_throw_commandNotHandledException()
             {
        
                 var passed = _bus.TryFire(new TestCommands.Command1(Guid.NewGuid(), null), out var response);
        
                 Assert.False(passed, "Expected false return");
                 Assert.IsType(typeof(Fail), response);
                 Assert.IsType(typeof(CommandNotHandledException), ((Fail)response).Exception);
             }
             [Fact]
             public void try_fire_slow_commands_should_return_timeout()
             {
                 long gotCmd1 = 0;
        
                 _bus.Subscribe(new AdHocCommandHandler<TestCommands.Command1>(
                        cmd =>
                        {
                            Interlocked.Exchange(ref gotCmd1, 1);
                            SpinWait.SpinUntil(() => false, 1000);
                            return true;
                        }));
        
                 var passed = _bus.TryFire(
                     new TestCommands.Command1(Guid.NewGuid(), null),
                     out var response,
                     TimeSpan.FromSeconds(2),
                     TimeSpan.FromMilliseconds(2));
        
                 Assert.False(passed, "Expected false return");
                 Assert.IsType(typeof(Fail), response);//,$"Expected 'Fail' got {response.GetType().Name}");
                 Assert.IsType(typeof(CommandTimedOutException), ((Fail)response).Exception);
                 Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotCmd1) == 1, msg: "Expected Cmd1 handled");
        
             }
        
             [Fact(Skip = "Connected bus scenarios currently disabled")]
             public void try_fire_oversubscribed_commands_should_return_false()
             {
        
                 var bus2 = new CommandBus("remote");
                 // ReSharper disable once UnusedVariable
                 var conn = new BusConnector(_bus, bus2);
                 long gotCmd = 0;
                 long processedCmd = 0;
                 long cancelPublished = 0;
                 long cancelReturned = 0;
        
                 _bus.Subscribe(new AdHocCommandHandler<TestCommands.Command1>(
                      cmd =>
                      {
                          Interlocked.Increment(ref gotCmd);
                          Task.Delay(250).Wait();
                          Interlocked.Increment(ref processedCmd);
                          return true;
                      }));
                 bus2.Subscribe(new AdHocCommandHandler<TestCommands.Command1>(
                         cmd =>
                         {
                             Interlocked.Increment(ref gotCmd);
                             Task.Delay(250).Wait();
                             Interlocked.Increment(ref processedCmd);
                             return true;
                         }));
                 _bus.Subscribe(new AdHocCommandHandler<TestCommands.Command2>(
                      cmd =>
                      {
                          Interlocked.Increment(ref gotCmd);
                          Task.Delay(250).Wait();
                          Interlocked.Increment(ref processedCmd);
                          return true;
                      }));
                 bus2.Subscribe(new AdHocHandler<Canceled>(c => Interlocked.Increment(ref cancelPublished)));
                 bus2.Subscribe(new AdHocHandler<Fail>(
                     c =>
                     {
                         if (c.Exception is CommandCanceledException)
                             Interlocked.Increment(ref cancelReturned);
                     }));
                 var timer = Stopwatch.StartNew();
                 var passed = _bus.TryFire(
                     new TestCommands.Command1(Guid.NewGuid(), null), out var response, TimeSpan.FromMilliseconds(1500));
                 timer.Stop();
                 Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotCmd) <= 1,
                     msg: "Expected command received no more than once, got " + gotCmd);
                 Assert.True(timer.ElapsedMilliseconds < 1000, "Expected failure before task completion.");
                 Assert.IsOrBecomesTrue(() => Interlocked.Read(ref processedCmd) == 0,
                      msg: "Expected command failed before handled; got " + processedCmd);
        
                 Assert.False(passed, "Expected false return");
                 Assert.IsType(typeof(Fail), response);
                 Assert.IsType(typeof(CommandOversubscribedException), ((Fail)response).Exception);
                 Assert.IsOrBecomesTrue(() => Interlocked.Read(ref processedCmd) <= 1, 1500, msg: "Expected command handled no more than once, actual" + processedCmd);
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
        
                 var bus2 = new CommandBus("remote");
                 // ReSharper disable once UnusedVariable
                 var conn = new BusConnector(_bus, bus2);
                 long processedCmd = 0;
                 long gotAck = 0;
                 _bus.Subscribe(new AdHocCommandHandler<TestCommands.Command1>(
                      cmd =>
                      {
                          Interlocked.Increment(ref processedCmd);
                          Task.Delay(1000).Wait();
                          return true;
                      }));
                 bus2.Subscribe(new AdHocCommandHandler<TestCommands.Command1>(
                       cmd =>
                       {
                           Interlocked.Increment(ref processedCmd);
                           Task.Delay(100).Wait();
                           return true;
                       }));
                 _bus.Subscribe(
                             new AdHocHandler<AckCommand>(cmd => Interlocked.Increment(ref gotAck)));
        
                 Assert.Throws<CommandOversubscribedException>(() =>
                             _bus.Fire(new TestCommands.Command1(Guid.NewGuid(), null)));
        
                 Assert.IsOrBecomesTrue(
                             () => Interlocked.Read(ref gotAck) == 2,
                             msg: "Expected command Acked twice, got " + gotAck);
        
                 Assert.IsOrBecomesTrue(
                             () => Interlocked.Read(ref processedCmd) <= 1,
                             msg: "Expected command handled once or less, actual " + processedCmd);
             }
             [Fact]
             public void try_fire_commands_should_not_call_other_commands()
             {
        
                 long gotCmd = 0;
        
        
                 _bus.Subscribe(new AdHocCommandHandler<TestCommands.Command1>(
                      cmd =>
                      {
                          Interlocked.Increment(ref gotCmd);
                          return true;
                      }));
                 _bus.Subscribe(new AdHocCommandHandler<TestCommands.Command2>(
                       cmd =>
                       {
                           Interlocked.Increment(ref gotCmd);
                           return true;
                       }));
        
                 var passed = _bus.TryFire(
                     new TestCommands.Command1(Guid.NewGuid(), null), out var response, TimeSpan.FromMilliseconds(1500));
        
                 Assert.True(passed, "Expected false return");
                 Thread.Sleep(100); //let other threads complete
                 Assert.IsOrBecomesTrue(() => Interlocked.Read(ref gotCmd) == 1,
                     msg: "Expected command received once, got " + gotCmd);
                 Assert.IsType(typeof(Success), response);
        
             }
             [Fact]
             public void unsubscribe_should_remove_handler()
             {
        
                 long proccessedCmd = 0;
                 var hndl = new AdHocCommandHandler<TestCommands.Command1>(
                      cmd =>
                      {
                          Interlocked.Increment(ref proccessedCmd);
                          return true;
                      });
                 _bus.Subscribe(hndl);
                 _bus.Fire(new TestCommands.Command1(Guid.NewGuid(), null));
                 _bus.Unsubscribe(hndl);
                 Assert.Throws<CommandNotHandledException>(() =>
                      _bus.Fire(new TestCommands.Command1(Guid.NewGuid(), null)));
                 Assert.IsOrBecomesTrue(
                     () => Interlocked.Read(ref proccessedCmd) == 1,
                     msg: "Expected command handled once, got" + proccessedCmd);
             }
             [Fact]
             public void can_resubscribe_handler()
             {
        
                 long proccessedCmd = 0;
                 var hndl = new AdHocCommandHandler<TestCommands.Command1>(
                      cmd =>
                      {
                          Interlocked.Increment(ref proccessedCmd);
                          return true;
                      });
                 _bus.Subscribe(hndl);
                 Assert.IsOrBecomesTrue(() => _bus.HasSubscriberFor<TestCommands.Command1>());
        
                 _bus.Fire(new TestCommands.Command1(Guid.NewGuid(), null));
                 _bus.Unsubscribe(hndl);
                 Assert.IsOrBecomesFalse(() => _bus.HasSubscriberFor<TestCommands.Command1>());
        
                 Assert.Throws<CommandNotHandledException>(
                     () => _bus.Fire(new TestCommands.Command1(Guid.NewGuid(), null)));
                 _bus.Subscribe(hndl);
                 Assert.IsOrBecomesTrue(() => _bus.HasSubscriberFor<TestCommands.Command1>());
        
                 _bus.Fire(new TestCommands.Command1(Guid.NewGuid(), null));
                 Assert.IsOrBecomesTrue(
                     () => Interlocked.Read(ref proccessedCmd) == 2,
                     msg: "Expected command handled twice, got" + proccessedCmd);
             }
             [Fact]
             public void unsubscribe_should_not_remove_other_handlers()
             {
        
                 long proccessedCmd = 0;
                 var hndl = new AdHocCommandHandler<TestCommands.Command1>(
                      cmd =>
                      {
                          Interlocked.Increment(ref proccessedCmd);
                          return true;
                      });
                 _bus.Subscribe(hndl);
                 _bus.Subscribe(new AdHocCommandHandler<TestCommands.Command2>(
                      cmd =>
                      {
                          Interlocked.Increment(ref proccessedCmd);
                          return true;
                      }));
                 _bus.Fire(new TestCommands.Command1(Guid.NewGuid(), null));
                 _bus.Unsubscribe(hndl);
                 Assert.Throws<CommandNotHandledException>(
                     () => _bus.Fire(new TestCommands.Command1(Guid.NewGuid(), null)));
        
                 _bus.Fire(new TestCommands.Command2(Guid.NewGuid(), null));
                 Assert.IsOrBecomesTrue(
                     () => Interlocked.Read(ref proccessedCmd) == 2,
                     msg: "Expected command handled twice, got" + proccessedCmd);
             }
             */
    }
}
