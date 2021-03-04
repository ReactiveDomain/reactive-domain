using ReactiveDomain.Messaging;
using System;

namespace ReactiveDomain.Users.Messages
{
    public class UserPolicyMsgs
    {
       
        public class AddPolicy : Command
        {           
            public readonly Guid UserId;           
            public readonly Guid PolicyId;              
            public AddPolicy(Guid userId, Guid policyId)
            {
                UserId = userId;
                PolicyId = policyId;
            }
        }
       
        public class PolicyAdded : Command
        {           
            public readonly Guid UserId;           
            public readonly Guid PolicyId;           
            public readonly Guid ApplicationId;           
            public PolicyAdded(Guid userId, Guid policyId)
            {
                UserId = userId;
                PolicyId = policyId;
            }
        }

        public class RemovePolicy : Command
        {           
            public readonly Guid UserId;           
            public readonly Guid PolicyId;           
            public RemovePolicy(Guid userId, Guid policyId)
            {
                UserId = userId;
                PolicyId = policyId;
            }
        }
       
        public class PolicyRemoved : Command
        {           
            public readonly Guid UserId;           
            public readonly Guid PolicyId;                
            public PolicyRemoved(Guid userId, Guid policyId)
            {
                UserId = userId;
                PolicyId = policyId;
            }
        }

        
        public class AddRole : Command
        {
            public readonly Guid UserId;
            public readonly Guid RoleId;

            public AddRole(Guid userId, Guid roleId)
            {
                UserId = userId;
                RoleId = roleId;
            }
        }
        public class RoleAdded : Event
        {
            public readonly Guid UserId;
            public readonly Guid RoleId;

            public RoleAdded(Guid userId, Guid roleId)
            {
                UserId = userId;
                RoleId = roleId;
            }
        }

        public class RemoveRole : Command
        {
            public readonly Guid UserId;
            public readonly Guid RoleId;

            public RemoveRole(Guid userId, Guid roleId)
            {
                UserId = userId;
                RoleId = roleId;
            }
        }

        public class RoleRemoved : Event
        {
            public readonly Guid UserId;
            public readonly Guid RoleId;

            public RoleRemoved(Guid userId, Guid roleId)
            {
                UserId = userId;
                RoleId = roleId;
            }
        }

    }
}
