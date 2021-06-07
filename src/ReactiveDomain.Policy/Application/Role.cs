using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Policy.Application
{
    /// <summary>
    /// Houses the role data populated by the role created handler.
    /// </summary>
    public class Role
    {
        /// <summary>
        /// The role ID.
        /// </summary>
        public Guid RoleId { get; private set; }
        /// <summary>
        /// The role name.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// The Id of the policy defining the roles.
        /// </summary>
        public Guid PolicyId { get; internal set; }

        // role has:
        // - Allowed Permissions
        // - Denied Permissions

        // methods:
        // - IsAllowed(ICommand cmd)

        private readonly HashSet<Type> _allowedPermissions = new HashSet<Type>();
        private readonly HashSet<Type> _deniedPermissions = new HashSet<Type>();
        private HashSet<Type> _effectivePermissions = new HashSet<Type>();

        public IReadOnlyList<Type> AllowedPermissions => _effectivePermissions.ToList().AsReadOnly();

        private readonly HashSet<Role> _parentRoles = new HashSet<Role>();
        /// <summary>
        /// Houses the role data populated by the role created handler.
        /// </summary>
        public Role(
            Guid roleId,
            string name,
            Guid policyId)
        {
            RoleId = roleId;
            Name = name;
            PolicyId = policyId;
        }

        public void AllowPermission(Type t)
        {
            var newTypes = new HashSet<Type>(MessageHierarchy.DescendantsAndSelf(t).Where(type => typeof(ICommand).IsAssignableFrom(type)));
            _allowedPermissions.UnionWith(newTypes);

            _effectivePermissions = new HashSet<Type>(_allowedPermissions);
            _effectivePermissions.ExceptWith(_deniedPermissions);
        }

        public void DenyPermission(Type t)
        {
            var newTypes = new HashSet<Type>(MessageHierarchy.DescendantsAndSelf(t).Where(type => typeof(ICommand).IsAssignableFrom(type)));
            _deniedPermissions.UnionWith(newTypes);

            _effectivePermissions = new HashSet<Type>(_allowedPermissions);
            _effectivePermissions.ExceptWith(_deniedPermissions);
        }

        public bool IsAllowed<T>() where T : class => _effectivePermissions.Any(ep => ep == typeof(T));

        internal void SetRoleId(Guid roleId, Guid policyId)
        {
            if (roleId == RoleId) return;
            if (roleId == Guid.Empty ) throw new ArgumentOutOfRangeException(nameof(roleId), "Cannot set roleId to guid.empty");
            if (policyId == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(policyId), "Cannot set policyId to guid.empty");
            if (RoleId != Guid.Empty) throw new InvalidOperationException("cannot change RoleID ");
            if (PolicyId != Guid.Empty) throw new InvalidOperationException("cannot change PolicyId ");
            RoleId = roleId;
            PolicyId = policyId;
        }
    }
}