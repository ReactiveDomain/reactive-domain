using System;
using System.Collections.Generic;
using System.Linq;

namespace ReactiveDomain.Users.ReadModels {
    /// <summary>
    /// Houses the role data populated by the role created handler.
    /// </summary>
    public class Role
    {
        /// <summary>
        /// The role ID.
        /// </summary>
        public Guid RoleId { get; }
        /// <summary>
        /// The role name.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// The application defining the roles.
        /// </summary>
        public ApplicationModel Application { get; }

        private readonly HashSet<string> _permissions = new HashSet<string>();
        public IReadOnlyList<string> Permissions => _permissions.ToList().AsReadOnly();
        private readonly HashSet<Role> _parentRoles = new HashSet<Role>();
        private readonly HashSet<Role> _childRoles = new HashSet<Role>();
        public IReadOnlyList<Role> ChildRoles => _childRoles.ToList().AsReadOnly();
        /// <summary>
        /// Houses the role data populated by the role created handler.
        /// </summary>
        public Role(
            Guid roleId,
            string name,
            ApplicationModel application)
        {
            RoleId = roleId;
            Name = name;
            Application = application;
        }

        public void AddPermission(string permission) {
            _permissions.Add(permission);
            foreach (var parentRole in _parentRoles) {
                parentRole.AddPermission(permission);
            }
        }
        public void AddChildRole(Role role) {
            _childRoles.Add(role);
            _permissions.UnionWith(role._permissions);
            role._parentRoles.Add(this);
        }
    }
}