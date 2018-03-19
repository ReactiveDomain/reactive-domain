using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests
{
	// ReSharper disable once InconsistentNaming
	public class when_sending_concurrent_commands :
		IHandleCommand<TestCommands.TestCommand>,
		IHandleCommand<TestCommands.TestCommand2>,
		IHandleCommand<TestCommands.TestCommand3>

	{
		private readonly TimeSpan _5Sec = TimeSpan.FromSeconds(5);
		private readonly CommandBus _bus;
		private long _cmd1Count;
		private long _cmd2Count;
		private long _cmd3Count;
		private readonly List<Command> _commands;
		private int _count;
		public when_sending_concurrent_commands()
		{
			_bus = new CommandBus("Test", false, _5Sec, _5Sec);
			_bus.Subscribe<TestCommands.TestCommand>(this);
			_bus.Subscribe<TestCommands.TestCommand2>(this);
			_bus.Subscribe<TestCommands.TestCommand3>(this);
			_commands = new List<Command>();
			_count = 5;
			for (int i = 0; i < _count; i++) {
				_commands.Add(new TestCommands.TestCommand(Guid.NewGuid(), null));
				_commands.Add(new TestCommands.TestCommand2(Guid.NewGuid(), null));
				_commands.Add(new TestCommands.TestCommand3(Guid.NewGuid(), null));
			}
		}

		[Fact]
		public void concurrent_commands_should_pass() {

			Parallel.ForEach(_commands, cmd => _bus.Fire(cmd));

			Assert.True(_cmd1Count == _count, $"Should be {_count}, found { _cmd1Count}");
			Assert.True(_cmd2Count == _count, $"Should be {_count}, found { _cmd2Count}");
			Assert.True(_cmd3Count == _count, $"Should be {_count}, found { _cmd3Count}");
			}

		public CommandResponse Handle(TestCommands.TestCommand command)
		{
			Interlocked.Increment(ref _cmd1Count);
			return command.Succeed();
		}

		public CommandResponse Handle(TestCommands.TestCommand2 command)
		{
			Interlocked.Increment(ref _cmd2Count);
			return command.Succeed();
		}

		public CommandResponse Handle(TestCommands.TestCommand3 command)
		{
			Interlocked.Increment(ref _cmd3Count);
			return command.Succeed();
		}
	}
}
