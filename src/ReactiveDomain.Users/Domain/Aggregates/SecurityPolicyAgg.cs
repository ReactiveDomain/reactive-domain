using System;
using System.Collections.Generic;
using System.Linq;

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
        }

        //Apply State
        private void Apply(RoleMsgs.RoleCreated @event) { if (@event.PolicyId == Id) _roles.Add(@event.RoleId, @event.Name); }
        private void Apply(RoleMsgs.RoleMigrated @event) { if (@event.PolicyId == Id) _roles.Add(@event.RoleId, @event.Name); }

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

        private class Role
        {
            //todo: do we need to implement this to model correct role invariants?
        }
    }

}
