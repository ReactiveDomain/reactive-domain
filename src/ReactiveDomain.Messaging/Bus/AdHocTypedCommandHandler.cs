using System;
using ReactiveDomain.Util;

namespace ReactiveDomain.Messaging.Bus
{
    public class AdHocTypedCommandHandler<T, R> : IHandleCommand<T> where T : Command
                                                                  where R : CommandResponse
    {
        private readonly Func<T, R> _handleCommand;
        private readonly bool _wrapExceptions;


        public AdHocTypedCommandHandler(
                    Func<T, R> handleCommandCommand,
                    bool wrapExceptions = true)
        {
            Ensure.NotNull(handleCommandCommand, "handle");
            _handleCommand = handleCommandCommand;
            _wrapExceptions = wrapExceptions;
        }

        public CommandResponse Handle(T command)
        {
            try
            {
                return _handleCommand(command);
            }
            catch (Exception ex)
            {
                if (_wrapExceptions)
                    return command.Fail(ex);
                throw;
            }

        }
    }
}