using System;
using System.Collections.Generic;
using System.Threading;
using ReactiveDomain.Bus;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Tests.Helpers
{
    public class CancelableTestCommandHandler :
        IHandleCommand<TestCommands.TestCommand>,
        IHandleCommand<TestCommands.TestCommand2>
    {
        // n.b. simple test class implementation only
        // use something more robust for production
        private readonly Dictionary<Type, HashSet<Guid>> _canceledCommands = new Dictionary<Type, HashSet<Guid>>();

        public CancelableTestCommandHandler()
        {
            _canceledCommands.Add(typeof(TestCommands.TestCommand), new HashSet<Guid>());
            _canceledCommands.Add(typeof(TestCommands.TestCommand2), new HashSet<Guid>());
        }
        public void RequestCancel(CancelCommand cancelRequest)
        {
            if ((cancelRequest.CommandType != typeof(TestCommands.TestCommand)) &&
                (cancelRequest.CommandType != typeof(TestCommands.TestCommand2)))
                return;

            _canceledCommands[cancelRequest.CommandType].Add(cancelRequest.CommandId);
        }

        public CommandResponse Handle(TestCommands.TestCommand command)
        {
            for (int i = 0; i < 5; i++)
            {
               
                if (_canceledCommands[typeof(TestCommands.TestCommand)].Contains(command.MsgId))
                  throw new CommandCanceledException(command);
                Thread.Sleep(50);
            }
            return command.Succeed();
        }

        public CommandResponse Handle(TestCommands.TestCommand2 command)
        {
            for (int i = 0; i < 5; i++)
            {
               
                if (_canceledCommands[typeof(TestCommands.TestCommand2)].Contains(command.MsgId))
                    throw new CommandCanceledException(command);
                Thread.Sleep(50);
            }
            return command.Succeed();
        }
    }
}
