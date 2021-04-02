using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Users.Scratch.PerApplication
{
    public class Role
    {
        private readonly HashSet<Type> _allowedActions = new HashSet<Type>();
        private readonly HashSet<Type> _prohibitedActions = new HashSet<Type>();
        private HashSet<Type> _effectiveActions = new HashSet<Type>();

        public Guid RoleId { get; private set; }
        public string Name { get; }
        public Guid PolicyId { get; internal set; }

        public IReadOnlyList<Type> AllowedActions => _effectiveActions.ToList().AsReadOnly();

        public Role(Guid roleId, string name, Guid policyId)
        {
            RoleId = roleId;
            Name = name;
            PolicyId = policyId;
        }

        /// <summary>
        /// As we are no longer defining permissions, but using commands within the system to provide access to items,
        /// allowing "actions" is a term to better describe what is, or is now, allowed by a user within the system.
        /// </summary>
        /// <param name="t"></param>
        public void AllowAction(Type t)
        {
            var newTypes = new HashSet<Type>(MessageHierarchy.DescendantsAndSelf(t).Where(type => typeof(ICommand).IsAssignableFrom(type)));
            _allowedActions.UnionWith(newTypes);

            _effectiveActions = new HashSet<Type>(_allowedActions);
            _effectiveActions.ExceptWith(_prohibitedActions);
        }

        /// <summary>
        /// Removes the capability of a user within this role to perform an action (command) within the system.
        /// </summary>
        /// <param name="t"></param>
        public void ProhibitAction(Type t)
        {
            var newTypes = new HashSet<Type>(MessageHierarchy.DescendantsAndSelf(t).Where(type => typeof(ICommand).IsAssignableFrom(type)));
            _prohibitedActions.UnionWith(newTypes);

            _effectiveActions = new HashSet<Type>(_allowedActions);
            _effectiveActions.ExceptWith(_prohibitedActions);
        }

        public bool IsAllowed<T>() where T : class => _effectiveActions.Any(ea => ea == typeof(T));
    }
}