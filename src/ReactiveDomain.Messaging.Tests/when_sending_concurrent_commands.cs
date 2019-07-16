using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests
{
    // ReSharper disable once InconsistentNaming
    public class when_sending_concurrent_commands :
        IHandleCommand<TestCommands.Command1>,
        IHandleCommand<TestCommands.Command2>,
        IHandleCommand<TestCommands.Command3>

    {
        private readonly TimeSpan _5Sec = TimeSpan.FromSeconds(5);
        private readonly Dispatcher _bus;
        private long _cmd1Count;
        private long _cmd2Count;
        private long _cmd3Count;
        private readonly List<Command> _commands;
        private int _count;
        public when_sending_concurrent_commands()
        {
            _bus = new Dispatcher("Test", 3, false, _5Sec, _5Sec);
            _bus.Subscribe<TestCommands.Command1>(this);
            _bus.Subscribe<TestCommands.Command2>(this);
            _bus.Subscribe<TestCommands.Command3>(this);
            _commands = new List<Command>();
            _count = 5;            
            for (int i = 0; i < _count; i++)
            {
                _commands.Add(new TestCommands.Command1());
                _commands.Add(new TestCommands.Command2());
                _commands.Add(new TestCommands.Command3());
            }
        }

        [Fact]
        public void concurrent_commands_should_pass()
        {

            Parallel.ForEach(_commands, cmd => _bus.Send(cmd));

            Assert.True(_cmd1Count == _count, $"Should be {_count}, found { _cmd1Count}");
            Assert.True(_cmd2Count == _count, $"Should be {_count}, found { _cmd2Count}");
            Assert.True(_cmd3Count == _count, $"Should be {_count}, found { _cmd3Count}");
        }

        public CommandResponse Handle(TestCommands.Command1 command)
        {
            Interlocked.Increment(ref _cmd1Count);
            return command.Succeed();
        }

        public CommandResponse Handle(TestCommands.Command2 command)
        {
            Interlocked.Increment(ref _cmd2Count);
            return command.Succeed();
        }

        public CommandResponse Handle(TestCommands.Command3 command)
        {
            Interlocked.Increment(ref _cmd3Count);
            return command.Succeed();
        }
    }
}
