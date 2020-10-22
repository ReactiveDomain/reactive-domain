using System;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Policy.Messages
{
    public class PolicyUserMsgs
    {
        public class PolicyUserAdded : Command
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

            public AddRole(Guid policyUserId, string roleName)
            {
                PolicyUserId = policyUserId;
                RoleName = roleName;
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

            public RemoveRole(Guid policyUserId, string roleName)
            {
                PolicyUserId = policyUserId;
                RoleName = roleName;
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
    }
}
