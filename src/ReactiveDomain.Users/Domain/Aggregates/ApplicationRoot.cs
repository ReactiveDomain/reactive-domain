using System;
using System.Collections.Generic;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Util;

namespace ReactiveDomain.Users.Domain.Aggregates
{
    /// <summary>
    /// Aggregate for a Application.
    /// </summary>
    public class ApplicationRoot : AggregateRoot
    {
        private readonly Dictionary<Guid, string> _roles = new Dictionary<Guid, string>();
        private readonly Dictionary<Guid, string> _permissions = new Dictionary<Guid, string>();

        private ApplicationRoot()
        {
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            Register<ApplicationMsgs.ApplicationCreated>(Apply);
            Register<RoleMsgs.RoleCreated>(Apply);
            Register<RoleMsgs.RoleMigrated>(Apply);
            Register<RoleMsgs.ChildRoleAssigned>(Apply);
            Register<RoleMsgs.PermissionAdded>(Apply);
            Register<RoleMsgs.PermissionAssigned>(Apply);

        }

        private void Apply(ApplicationMsgs.ApplicationCreated evt) => Id = evt.ApplicationId;
        private void Apply(RoleMsgs.RoleCreated evt) => _roles.Add(evt.RoleId, evt.Name);
        private void Apply(RoleMsgs.RoleMigrated evt) => _roles.Add(evt.RoleId, evt.Name);
        private void Apply(RoleMsgs.ChildRoleAssigned evt) { /*todo:do we need to track this? see contained role class*/}
        private void Apply(RoleMsgs.PermissionAdded evt) => _permissions.Add(evt.PermissionId, evt.PermissionName);
        private void Apply(RoleMsgs.PermissionAssigned evt) { /*todo:do we need to track this? see contained role class */}

        /// <summary>
        /// Create a new Application.
        /// </summary>
        public ApplicationRoot(
            Guid id,
            string name,
            string version)
            : this()
        {
            Ensure.NotEmptyGuid(id, nameof(id));
            Ensure.NotNullOrEmpty(name, nameof(name));
            Ensure.NotNullOrEmpty(version, nameof(version));
            Raise(new ApplicationMsgs.ApplicationCreated(
                         id,
                         name,
                         version));
        }

        /// <summary>
        /// Retire an application that is no longer in use.
        /// </summary>
        public void Retire()
        {
            // Event should be idempotent in RMs, so no validation necessary.
            Raise(new ApplicationMsgs.ApplicationRetired(Id));
        }

        /// <summary>
        /// Re-activate a retired application that is being put back into use.
        /// </summary>
        public void Unretire() {
            // Event should be idempotent in RMs, so no validation necessary.
            Raise(new ApplicationMsgs.ApplicationUnretired(Id));
        }

        /// <summary>
        /// Add a new role.
        /// </summary>
        public void AddRole(
            Guid roleId,
            string roleName)
        {
            Ensure.NotEmptyGuid(roleId, nameof(roleId));
            Ensure.NotNullOrEmpty(roleName, nameof(roleName));
            if (_roles.ContainsValue(roleName) || _roles.ContainsKey(roleId))
            {
                throw new InvalidOperationException($"Cannot add duplicate role. RoleName: {roleName} RoleId:{roleId}");
            }

            Raise(new RoleMsgs.RoleCreated(
                roleId,
                roleName,
                Id));
        }
        /// <summary>
        /// assign a child role
        /// </summary>
        public void AssignChildRole(
            Guid parentRoleId,
            Guid childRoleId)
        {
            if (!_roles.ContainsKey(parentRoleId) || !_roles.ContainsKey(childRoleId))
            {
                throw new InvalidOperationException($"Cannot add child role, role not found Parent Role: {parentRoleId} Child Role: {childRoleId}");
            }
            //todo: need some sort of check here, do we need a child domain entity for role to track this?
            Raise(new RoleMsgs.ChildRoleAssigned(
                parentRoleId,
                childRoleId,
                Id));
        }

        public void AddPermission(Guid permissionId, string name) {
            Ensure.NotEmptyGuid(permissionId, nameof(permissionId));
            Ensure.NotNullOrEmpty(name, nameof(name));
            if (_roles.ContainsValue(name) || _roles.ContainsKey(permissionId))
            {
                throw new InvalidOperationException($"Cannot add duplicate permission. Name: {name} Id:{permissionId}");
            }

            Raise(new RoleMsgs.RoleCreated(
                permissionId,
                name,
                Id));
        }
        public void AssignPermission(
            Guid roleId,
            Guid permissionId)
        {
            if (!_roles.ContainsKey(roleId) || !_permissions.ContainsKey(permissionId))
            {
                throw new InvalidOperationException($"Cannot assign permission role, role or permission not found Role: {roleId} Permission: {permissionId}");
            }
            //todo: need some sort of check here, do we need a child domain entity for role to track this?
            Raise(new RoleMsgs.PermissionAssigned(
                roleId,
                permissionId,
                Id));
        }
        private class Role {
            //todo: do we need to implement this to model correct role invariants?
        }
    }
}
