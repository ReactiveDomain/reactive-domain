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
        public IReadOnlyList<Guid> Roles => _roles.Keys.ToList();
        //todo: set these from an apply method
        public string ClientId { get; private set; }
        public Guid AppId => base.Id;

        //n.b. this method is called only inside an apply handler in the root aggregate
        // so setting values is ok, but raising events is not
        // the create event is raised in the root aggregate
        public SecurityPolicyAgg(
            Guid policyId,
            string clientId,
            SecuredApplicationAgg root)
            : base(policyId, root)
        {
            Register<RoleMsgs.RoleCreated>(Apply);
            Register<RoleMsgs.RoleMigrated>(Apply);   
            ClientId = clientId;
        }

        //Apply State only if it applies to my id
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
