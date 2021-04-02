using System;
using System.Collections.Generic;
using ReactiveDomain.Messaging;
using ReactiveDomain.Users.Scratch.PerApplication;

namespace ReactiveDomain.Users.Scratch
{
    public class SecuredApplication : AggregateRoot
    {
        private readonly Dictionary<Guid, SecurityPolicy> _policies = new Dictionary<Guid, SecurityPolicy>();
        private readonly HashSet<string> _policyNames = new HashSet<string>();

        public SecuredApplication(Guid id, string clientId, string clientSecret, ICorrelatedMessage msg = default) : base(msg)
        {
            RegisterEvents();
            Raise(new SecuredApplicationMsgs.ApplicationCreated(id, clientId, clientSecret));
        }

        public SecuredApplication(ICorrelatedMessage msg) : base(msg)
        {
            RegisterEvents();
        }

        public SecuredApplication() : base(null)
        {
            RegisterEvents();
        }

        void RegisterEvents()
        {
            Register<SecuredApplicationMsgs.ApplicationCreated>(created => Id = created.SecuredApplicationId);
            Register<SecuredApplicationMsgs.PolicyCreated>(created =>
            {
                _policies.Add(created.PolicyId, new SecurityPolicy(created.PolicyId, created.PolicyName, this));
                _policyNames.Add(created.PolicyName);
            });
        }

        /// <summary>
        /// Sets the role that grants access to the application for users. If the user does not have this role, they
        /// are directed to a "forbidden" page or similar.
        /// </summary>
        /// <param name="roleName"></param>
        public void SetAccessRole(string roleName)
        {
            Raise(new SecuredApplicationMsgs.AccessRoleSet(Id, roleName));
        }

        public SecurityPolicy AddPolicy(Guid policyId, string policyName)
        {
            Raise(new SecuredApplicationMsgs.PolicyCreated(Id, policyId, policyName));
            return _policies[policyId];
        }

        // These two methods feel more "at home" for managing user roles, as the roles are application specific.
        // Needs confirmation from CC.
        public void AddRole(User user, IEnumerable<Role> roles) { }
        public void RemoveRoles(User user, IEnumerable<Role> roles) { }
    }
}