using System;
using ReactiveDomain.Util;

namespace ReactiveDomain.Messaging.Bus
{
    public class AdHocCommandHandler<T> : IHandleCommand<T> where T : Command
    {
        private readonly Func<T, bool> _handleCommand;
        private readonly bool _wrapExceptions;
        private Guid _currentCommand = Guid.Empty;


        public AdHocCommandHandler(
                        Func<T, bool> handleCommandCommand,
                        bool wrapExceptions = true)
        {
            Ensure.NotNull(handleCommandCommand, "handle");
            _handleCommand = handleCommandCommand;
            _wrapExceptions = wrapExceptions;
        }

        public CommandResponse Handle(T command)
        {
            bool passed;
            _currentCommand = command.MsgId;
            try
            {
                if (command.IsCanceled)
                    return command.Canceled();

                passed = _handleCommand(command);
            }
            catch (Exception ex)
            {
                if (_wrapExceptions)
                    return command.Fail(ex);
                throw;
            }
            if (command.IsCanceled)
                return command.Canceled();

            return passed ? command.Succeed() : command.Fail();
        }
    }
}