using ReactiveDomain.Messaging;
using System;

namespace ReactiveDomain.Users.Policy
{
    public class CommandPermission : Permission
    {
        public CommandPermission(Guid id, ICommand command, Guid policyId, Func<string, Type> typeFinder) :
            base(id, $"{command.GetType().FullName},{command.GetType().Assembly.GetName()}", policyId)
        {
            _command = command.GetType();
            _typeFinder = typeFinder;
        }
        private Type _command;
        private readonly Func<string, Type> _typeFinder;

        public Type Command
        {
            get
            {
                if (_command == null)
                {
                    _command = _typeFinder(Name);
                }
                return _command;
            }
        }
    }
}
