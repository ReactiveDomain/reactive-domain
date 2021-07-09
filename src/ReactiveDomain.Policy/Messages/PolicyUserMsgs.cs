using System;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Policy.Messages
{
    public class PolicyUserMsgs
    {

        public class AddPolicyUser : Command
        {   
            public readonly Guid PolicyUserId;
            public readonly Guid UserId;
            public readonly Guid PolicyId;
            public readonly Guid ApplicationId;
            public AddPolicyUser(Guid policyUserId,Guid userId, Guid policyId, Guid applicationId) {
                PolicyUserId = policyUserId;
                UserId = userId;
                PolicyId = policyId;
                ApplicationId = applicationId;
            }
        }
        public class PolicyUserAdded : Event
        {
            public readonly Guid PolicyUserId;
            public readonly Guid UserId;
            public readonly Guid PolicyId;
            public PolicyUserAdded(Guid policyUserId,Guid userId, Guid policyId) {
                PolicyUserId = policyUserId;
                UserId = userId;
                PolicyId = policyId;
            }
        }
       
        public class AddRole : Command
        {
            public readonly Guid PolicyUserId;
            public readonly string RoleName;
            public readonly Guid RoleId;
            public AddRole(Guid policyUserId, string roleName, Guid roleId)
            {
                PolicyUserId = policyUserId;
                RoleName = roleName;
                RoleId = roleId;
            }
        }
        public class RoleAdded : Event
        {
            public readonly Guid PolicyUserId;
            public readonly Guid RoleId;
            public readonly string RoleName;

            public RoleAdded(Guid policyUserId, Guid roleId, string roleName)
            {
                PolicyUserId = policyUserId;
                RoleId = roleId;
                RoleName = roleName;
            }
        }

        public class RemoveRole : Command
        {
            public readonly Guid PolicyUserId;
            public readonly string RoleName;
            public readonly Guid RoleId;

            public RemoveRole(Guid policyUserId, string roleName, Guid roleId)
            {
                PolicyUserId = policyUserId;
                RoleName = roleName;
                RoleId = roleId;
            }
        }

        public class RoleRemoved : Event
        {
            public readonly Guid PolicyUserId;
            public readonly Guid RoleId;
            public readonly string RoleName;

            public RoleRemoved(Guid policyUserId, Guid roleId, string roleName)
            {
                PolicyUserId = policyUserId;
                RoleId = roleId;
                RoleName = roleName;
            }
        }
        public class DeactivateUser : Command
        {
            public readonly Guid PolicyUserId;

            public DeactivateUser(Guid policyUserId)
            {
                PolicyUserId = policyUserId;
            }
        }

        public class UserDeactivated : Event
        {
            public readonly Guid PolicyUserId;

            public UserDeactivated(Guid policyUserId)
            {
                PolicyUserId = policyUserId;
            }
        }
        public class ReactivateUser : Command
        {
            public readonly Guid PolicyUserId;

            public ReactivateUser(Guid policyUserId)
            {
                PolicyUserId = policyUserId;
            }
        }

        public class UserReactivated : Event
        {
            public readonly Guid PolicyUserId;

            public UserReactivated(Guid policyUserId)
            {
                PolicyUserId = policyUserId;
            }
        }
    }
}
