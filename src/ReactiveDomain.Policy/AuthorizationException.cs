using System;

namespace ReactiveDomain.Policy
{
    public  class AuthorizationException:Exception
    {
        public Type Command { get; }
        public AuthorizationException(Type command, string message):base($"{command.Name} not authorized {message}")
        {
            Command = command;
        }       
    }
}
