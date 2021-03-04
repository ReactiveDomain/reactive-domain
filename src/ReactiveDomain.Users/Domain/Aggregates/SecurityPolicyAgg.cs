using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ReactiveDomain.Foundation.Domain;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Util;

namespace ReactiveDomain.Users.Domain.Aggregates
{

    public class SecurityPolicyAgg : ChildEntity
    {
        private readonly Dictionary<Guid, string> _roles = new Dictionary<Guid, string>();
        private readonly Dictionary<Guid, string> _permissions = new Dictionary<Guid, string>();
        public IReadOnlyList<Guid> Roles => _roles.Keys.ToList();

        public readonly string PolicyName;

        public SecurityPolicyAgg(
            Guid policyId, 
            string policyName,
            SecuredApplicationAgg root)
            : base(policyId, root) {
            PolicyName = policyName;
            Register<RoleMsgs.RoleCreated>(Apply);
            Register<RoleMsgs.RoleMigrated>(Apply);
            Register<RoleMsgs.ChildRoleAssigned>(Apply);
            Register<RoleMsgs.PermissionAdded>(Apply);
            Register<RoleMsgs.PermissionAssigned>(Apply);
        }

        //Apply State
        private void Apply(RoleMsgs.RoleCreated @event) { if (@event.PolicyId == Id) _roles.Add(@event.RoleId, @event.Name); }
        private void Apply(RoleMsgs.RoleMigrated @event) { if (@event.PolicyId == Id) _roles.Add(@event.RoleId, @event.Name); }
        private void Apply(RoleMsgs.ChildRoleAssigned @event) { /*todo:do we need to track this? see contained role class*/}
        private void Apply(RoleMsgs.PermissionAdded @event) { if (@event.PolicyId == Id) _permissions.Add(@event.PermissionId, @event.PermissionName); }
        private void Apply(RoleMsgs.PermissionAssigned @event) { /*todo:do we need to track this? see contained role class */}

        //Public methods
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

        public void AddPermission(Guid permissionId, string name)
        {
            Ensure.NotEmptyGuid(permissionId, nameof(permissionId));
            Ensure.NotNullOrEmpty(name, nameof(name));
            if (_permissions.ContainsValue(name) || _permissions.ContainsKey(permissionId))
            {
                throw new InvalidOperationException($"Cannot add duplicate permission. Name: {name} Id:{permissionId}");
            }

            Raise(new RoleMsgs.PermissionAdded(
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
        private class Role
        {
            //todo: do we need to implement this to model correct role invariants?
        }
    }

}
