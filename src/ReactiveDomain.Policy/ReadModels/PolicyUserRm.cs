using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Policy.Application;
using ReactiveDomain.Policy.Messages;
using ReactiveDomain.Users;
using ReactiveDomain.Users.ReadModels;


namespace ReactiveDomain.Policy.ReadModels
{
    /// <summary>
    /// A read model that contains a list of Policy Users. 
    /// </summary>
    public class PolicyUserRm :
        ReadModelBase,
        IHandle<PolicyUserMsgs.PolicyUserAdded>,
        IHandle<PolicyUserMsgs.RoleAdded>,
        IHandle<PolicyUserMsgs.RoleRemoved>,
        IHandle<PolicyUserMsgs.UserDeactivated>,
        IHandle<PolicyUserMsgs.UserReactivated>
    {
        /// <summary>
        /// Create a read model for getting information about Policy Users.
        /// </summary>
        public PolicyUserRm(IConfiguredConnection conn)
            : base(nameof(PolicyUserRm), () => conn.GetListener(nameof(PolicyUserRm)))
        {
            //set handlers
            EventStream.Subscribe<PolicyUserMsgs.PolicyUserAdded>(this);
            EventStream.Subscribe<PolicyUserMsgs.RoleAdded>(this);
            EventStream.Subscribe<PolicyUserMsgs.RoleRemoved>(this);
            EventStream.Subscribe<PolicyUserMsgs.UserDeactivated>(this);
            EventStream.Subscribe<PolicyUserMsgs.UserReactivated>(this);

            //read
            long? checkpoint;
            using (var reader = conn.GetReader(nameof(PolicyUserRm), this))
            {
                reader.EventStream.Subscribe<Message>(this);
                reader.Read<Domain.PolicyUser>();
                checkpoint = reader.Position;
            }
            //subscribe
            Start<Domain.PolicyUser>(checkpoint);
        }
        //todo: consider complex types to model this
        public readonly Dictionary<Guid, Guid> UserByPolicyUser = new Dictionary<Guid, Guid>();
        private readonly Dictionary<Guid, Guid> _policyByPolicyUser = new Dictionary<Guid, Guid>();

        public readonly Dictionary<Guid, HashSet<string>> RolesByPolicyUser = new Dictionary<Guid, HashSet<string>>();
        public readonly Dictionary<Guid, HashSet<Guid>> UsersByPolicy = new Dictionary<Guid, HashSet<Guid>>();
        public readonly Dictionary<Guid, HashSet<Guid>> PoliciesByUser = new Dictionary<Guid, HashSet<Guid>>();
        public readonly Dictionary<Guid, HashSet<Guid>> PolicyUsersByUserId = new Dictionary<Guid, HashSet<Guid>>();

        public void Handle(PolicyUserMsgs.PolicyUserAdded @event)
        {
            if (!PolicyUsersByUserId.TryGetValue(@event.UserId, out var policyUsers))
            {
                policyUsers = new HashSet<Guid>();
                PolicyUsersByUserId.Add(@event.UserId, policyUsers);
            }
            policyUsers.Add(@event.PolicyUserId);

            if (!UsersByPolicy.TryGetValue(@event.PolicyId, out var users))
            {
                users = new HashSet<Guid>();
                UsersByPolicy.Add(@event.PolicyId, users);
            }
            users.Add(@event.UserId);

            if (!PoliciesByUser.TryGetValue(@event.UserId, out var policies))
            {
                policies = new HashSet<Guid>();
                PoliciesByUser.Add(@event.UserId, policies);
            }
            policies.Add(@event.PolicyId);

            if (!UserByPolicyUser.ContainsKey(@event.PolicyUserId))
            {
                UserByPolicyUser.Add(@event.PolicyUserId, @event.UserId);
            }
            else //this shouldn't happen, but let's cover the bases
            {
                UserByPolicyUser[@event.PolicyUserId] = @event.UserId;
            }

            if (!_policyByPolicyUser.ContainsKey(@event.PolicyUserId))
            {
                _policyByPolicyUser.Add(@event.PolicyUserId, @event.PolicyId);
            }
            else //this shouldn't happen, but let's cover the bases
            {
                _policyByPolicyUser[@event.PolicyUserId] = @event.PolicyId;
            }

            if (!RolesByPolicyUser.TryGetValue(@event.PolicyUserId, out var roles))
            {
                RolesByPolicyUser.Add(@event.PolicyUserId, new HashSet<string>());
            }
        }

        public void Handle(PolicyUserMsgs.RoleAdded @event)
        {
            if (RolesByPolicyUser.TryGetValue(@event.PolicyUserId, out var roles)) {
                roles.Add(@event.RoleName);
            }
        }

        public void Handle(PolicyUserMsgs.RoleRemoved @event)
        {
            if (RolesByPolicyUser.TryGetValue(@event.PolicyUserId, out var roles))
            {
                roles.Remove(@event.RoleName);
            }
        }

        public void Handle(PolicyUserMsgs.UserDeactivated @event)
        {
            var userId = UserByPolicyUser[@event.PolicyUserId];
            var policyId = _policyByPolicyUser[@event.PolicyUserId];

            if (UsersByPolicy.TryGetValue(policyId, out var users))
            {
                users.Remove(userId);
            }
            if (PoliciesByUser.TryGetValue(userId, out var policies))
            {
                policies.Remove(policyId);
            }
            if (PolicyUsersByUserId.TryGetValue(userId, out policies))
            {
                policies.Remove(policyId);
            }
        }
        public void Handle(PolicyUserMsgs.UserReactivated @event)
        {
            var userId = UserByPolicyUser[@event.PolicyUserId];
            var policyId = _policyByPolicyUser[@event.PolicyUserId];
            if (UsersByPolicy.TryGetValue(policyId, out var users))
            {
                users.Add(userId);
            }
            if (PoliciesByUser.TryGetValue(userId, out var policies))
            {
                policies.Add(policyId);
            }
            if (PolicyUsersByUserId.TryGetValue(userId, out policies))
            {
                policies.Add(policyId);
            }
        }
    }
}
