using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Util;

namespace ReactiveDomain.Users.Policy
{
    public class Permission
    {
        private readonly Type _t;
        /// <summary>
        /// The name of the command which is enveloped by this permission definition.
        /// </summary>
        public string Name => _t.Name;

        /// <summary>
        /// The full name of the command which is enveloped by this permission definition.
        /// </summary>
        public string FullName => _t.FullName;

        /// <summary>
        /// determines if the source type is equivalent of the type being represented by this permission.
        /// <remarks>Use this method as part of a linq expression to determine if the permission exists for the command.</remarks>
        /// </summary>
        /// <param name="source">The type of the command that is attempting to be executed.</param>
        /// <returns>True - The types are equivalent; False - The types are not equivalent.</returns>
        public bool Matches(Type source) => _t == source;

        public bool Matches<T>() => Matches(typeof(T));

        public Permission(Type t)
        {
            Ensure.NotNull(t, nameof(t));
            if (!typeof(ICommand).IsAssignableFrom(t)) throw new ArgumentException("Type must implement `ICommand`.");
            _t = t;
        }
    }
}
