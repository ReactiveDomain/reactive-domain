using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Policy
{
    public static class Permissions
    {
        private static readonly Type CommandType = typeof(Command);

        public static Permission[] GetCommandPermissions(Type type)
        {
            return GetCommands(type).Select(t => new Permission(t)).ToArray();
        }

        public static IEnumerable<Type> GetCommands(Type type)
        {
            return type.GetNestedTypes().Where(t => CommandType.IsAssignableFrom(t));
        }
    }
}
