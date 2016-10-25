using System;
using System.Reactive;
using ReactiveDomain.Messaging;
using ReactiveDomain.Util;

namespace ReactiveDomain.Bus
{
    public class AdHocTypedCommandHandler<T, R> : IHandleCommand<T> where T : Command
                                                                  where R : CommandResponse
    {
        private readonly Func<T, R> _handleCommand;
        private readonly Func<CancelCommand, Unit> _handleCancel;
        private readonly bool _wrapExceptions;


        public AdHocTypedCommandHandler(
                    Func<T, R> handleCommandCommand,
                     Func<CancelCommand, Unit> handleCancel = null,
                    bool wrapExceptions = true)
        {
            Ensure.NotNull(handleCommandCommand, "handle");
            _handleCommand = handleCommandCommand;
            _handleCancel = handleCancel;
            _wrapExceptions = wrapExceptions;
            if (_handleCancel == null) _handleCancel = c => Unit.Default;
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

        public void RequestCancel(CancelCommand cancelRequest)
        {
            try
            {
                _handleCancel(cancelRequest);
            }
            catch (Exception)
            {
            }
        }
    }
}