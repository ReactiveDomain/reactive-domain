using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ReactiveDomain.Messaging;
using ReactiveDomain.Policy.Messages;
using ReactiveDomain.Util;
[assembly: InternalsVisibleTo("ReactiveDomain.Identity")]
namespace ReactiveDomain.Policy.Domain
{
    /// <summary>
    /// Aggregate for a User in an application policy.
    /// </summary>
    internal class PolicyUser : AggregateRoot {

        private Guid _userId;
        private Guid _policyId;
        private readonly HashSet<(string name, Guid id)> _roles = new HashSet<(string name, Guid id)>();
        private HashSet<(string name, Guid id)> _rolesAtDeactivation = new HashSet<(string name, Guid id)>();
        public Guid PolicyId => _policyId;
        public Guid UserId => _userId;
        public HashSet<string> Roles => new HashSet<string>(_roles.Select(x => x.name));
        private bool _active;

        // ReSharper disable once UnusedMember.Local
        // used via reflection in the repository
        private PolicyUser()
        {
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            Register<PolicyUserMsgs.PolicyUserAdded>(Apply);
            Register<PolicyUserMsgs.RoleAdded>(Apply);
            Register<PolicyUserMsgs.RoleRemoved>(Apply);
            Register<PolicyUserMsgs.UserDeactivated>(Apply);
            Register<PolicyUserMsgs.UserReactivated>(Apply);
        }
        //Apply State Changes
        private void Apply(PolicyUserMsgs.PolicyUserAdded @event) {
            Id = @event.PolicyUserId;
            _policyId = @event.PolicyId;
            _userId = @event.UserId;
            _active = true;
        }

        private void Apply(PolicyUserMsgs.RoleAdded @event) {
            _roles.Add((@event.RoleName, @event.RoleId));
        }

        private void Apply(PolicyUserMsgs.RoleRemoved @event) {
            _roles.Remove((@event.RoleName, @event.RoleId));
        }

        private void Apply(PolicyUserMsgs.UserDeactivated @event) {
            _active = false;
            _rolesAtDeactivation = new HashSet<(string name, Guid id)>(_roles);
        }

        private void Apply(PolicyUserMsgs.UserReactivated @event) {
            _active = true;
        }

        //Public Methods

        /// <summary>
        /// Create a new Policy User.
        /// </summary>
        public PolicyUser(
            Guid id,
            Guid policyId,
            Guid userId,
            bool singleRole,
            ICorrelatedMessage source)
            : base(source)
        {
            Ensure.NotEmptyGuid(id, nameof(id));
            Ensure.NotEmptyGuid(policyId, nameof(policyId));
            Ensure.NotEmptyGuid(userId, nameof(userId));
            Ensure.NotEmptyGuid(source.CorrelationId, nameof(source));
            RegisterEvents();
            Raise(new PolicyUserMsgs.PolicyUserAdded(
                         id,
                         userId,
                         policyId,
                         singleRole));
        }

        public void AddRole(string roleName, Guid roleId) {
            Raise(new PolicyUserMsgs.RoleAdded(Id, roleId, roleName));
        }
        public void RemoveRole(string roleName, Guid roleId) {
            Raise(new PolicyUserMsgs.RoleRemoved(Id, roleId, roleName));
        }

        public void Deactivate() {
            if (!_active) { return; }
            Raise(new PolicyUserMsgs.UserDeactivated(Id));
            foreach (var role in _roles.ToArray()) {
                Raise(new PolicyUserMsgs.RoleRemoved(Id, role.id, role.name));
            }
        }

        public void Reactivate() {
            if (_active) { return; }
            Raise(new PolicyUserMsgs.UserReactivated(Id));
            if (_rolesAtDeactivation?.Any() ?? false) {
                foreach (var role in _rolesAtDeactivation) {
                    Raise(new PolicyUserMsgs.RoleAdded(Id, role.id, role.name));
                }
            }
        }
    }
}
