using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Policy.Messages;
using ReactiveDomain.Users.Domain;
using ReactiveDomain.Util;

namespace ReactiveDomain.Policy.Domain
{
    /// <summary>
    /// Aggregate for a User in an application policy.
    /// </summary>
    public class PolicyUser : AggregateRoot {

        private Guid _userId;
        private Guid _policyId;
        public Guid PolicyId => _policyId;
        public Guid UserId => _userId;
        // ReSharper disable once UnusedMember.Local
        // used via reflection in the repository
        private PolicyUser()
        {
            RegisterEvents();
        }

        private void RegisterEvents()
        {
          Register<PolicyUserMsgs.PolicyUserAdded>(Apply);
        }
        //Apply State Changes
        private void Apply(PolicyUserMsgs.PolicyUserAdded @event) {
            Id = @event.PolicyUserId;
            _policyId = @event.PolicyId;
            _userId = @event.UserId;
        }

        //Public Methods

        /// <summary>
        /// Create a new Application.
        /// </summary>
        public PolicyUser(
            Guid id,
            SecurityPolicy policy,
            User user,
            ICorrelatedMessage source)
            : base(source)
        {
            Ensure.NotEmptyGuid(id, nameof(id));
            Ensure.NotNull(policy, nameof(policy));
            Ensure.NotNull(user, nameof(user));
            Ensure.NotEmptyGuid(source.CorrelationId, nameof(source));
            RegisterEvents();
            Raise(new PolicyUserMsgs.PolicyUserAdded(
                         id,
                         user.Id,
                         policy.Id
                         ));
        }

        public void AddRole(string roleName, Guid roleId) {
            Raise(new PolicyUserMsgs.RoleAdded(Id,roleId,roleName));
        }
        public void RemoveRole(string roleName, Guid roleId) {
            Raise(new PolicyUserMsgs.RoleRemoved(Id,roleId,roleName));
        }

    }
}
