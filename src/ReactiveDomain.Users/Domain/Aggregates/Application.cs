using System;
using System.Collections.Generic;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Util;

namespace ReactiveDomain.Users.Domain.Aggregates
{
    /// <summary>
    /// Aggregate for a Application.
    /// </summary>
    public class Application : AggregateRoot
    {
        private readonly Dictionary<Guid, string> _roles = new Dictionary<Guid, string>();
         
        private Application()
        {
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            Register<ApplicationMsgs.ApplicationCreated>(Apply);
            Register<RoleMsgs.RoleCreated>(Apply);
            Register<RoleMsgs.RoleMigrated>(Apply);
            Register<RoleMsgs.RoleRemoved>(Apply);
            Register<RoleMsgs.ChildRoleAdded>(Apply);
        }

        private void Apply(ApplicationMsgs.ApplicationCreated evt) => Id = evt.ApplicationId;
        private void Apply(RoleMsgs.RoleCreated evt) => _roles.Add(evt.RoleId, evt.Name);
        private void Apply(RoleMsgs.RoleMigrated evt) => _roles.Add(evt.RoleId, evt.Name);
        private void Apply(RoleMsgs.RoleRemoved evt) => _roles.Remove(evt.RoleId);
        private void Apply(RoleMsgs.ChildRoleAdded evt) { /*todo:do we need to track this? */}

        /// <summary>
        /// Create a new Application.
        /// </summary>
        public Application(
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
        /// Add a new role.
        /// </summary>
        public void AddChildRole(
            Guid parentRoleId,
            Guid childRoleId)
        {
            if (!_roles.ContainsKey(parentRoleId) || !_roles.ContainsKey(childRoleId))
            {
                throw new InvalidOperationException($"Cannot add child role, role not found Parent Role: {parentRoleId} Child Role: {childRoleId}");
            }
            //todo: need some sort of check here, do we need a child domain entity for role to track this?
            Raise(new RoleMsgs.ChildRoleAdded(
                parentRoleId,
                childRoleId,
                Id));
        }

        //todo: do we need this capability? it opens a lot of edges around re-adding roles, for little apparent gain
        /// <summary>
        /// Remove a role.
        /// </summary>
        public void RemoveRole(Guid roleId)
        {
            if (!_roles.ContainsKey(roleId))
            {
                throw new InvalidOperationException($"Unknown RoleId {roleId}");
            }
            Raise(new RoleMsgs.RoleRemoved(Id));
        }
    }
}
