using System;
using System.Collections.Generic;
using System.Reactive;
using ReactiveDomain.Messaging;
using ReactiveDomain.Util;

namespace ReactiveDomain.Bus
{
    public class AdHocCommandHandler<T> : IHandleCommand<T> where T : Command
    {
        private readonly Func<T, bool> _handleCommand;
        private readonly Func<CancelCommand, Guid, Unit> _handleCancel;
        private readonly bool _wrapExceptions;
        private Guid _currentCommand = Guid.Empty;
        private readonly HashSet<Guid> _canceledCommands = new HashSet<Guid>();


        public AdHocCommandHandler(
                        Func<T, bool> handleCommandCommand,
                        Func<CancelCommand, Guid, Unit> handleCancel = null,
                        bool wrapExceptions = true)
        {
            Ensure.NotNull(handleCommandCommand, "handle");
            _handleCommand = handleCommandCommand;
            _handleCancel = handleCancel;
            _wrapExceptions = wrapExceptions;
            if (_handleCancel == null) _handleCancel = (c, g) => Unit.Default;
        }

        public CommandResponse Handle(T command)
        {
            bool passed;
            _currentCommand = command.MsgId;
            try
            {
                if (_canceledCommands.Contains(command.MsgId))
                    throw new CommandCanceledException(command);

                passed = _handleCommand(command);
            }
            catch (Exception ex)
            {
                if (_wrapExceptions)
                    return command.Fail(ex);
                throw;
            }
            return passed ? command.Succeed() : command.Fail();
        }
        public void RequestCancel(CancelCommand cancelRequest)
        {
            try
            {
                _canceledCommands.Add(cancelRequest.CommandId);
                _handleCancel(cancelRequest, _currentCommand);
            }
            catch
            {
                // ignored
            }
        }

    }
}