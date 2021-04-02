using System;

using ReactiveDomain.Messaging;

namespace ReactiveDomain.Users.Scratch
{
    public class SecuredApplicationMsgs
    {
        public class ApplicationCreated : Event
        {
            public readonly Guid SecuredApplicationId;
            public readonly string ClientId;
            public readonly string ClientSecret;

            public ApplicationCreated(Guid securedApplicationId, string clientId, string clientSecret)
            {
                SecuredApplicationId = securedApplicationId;
                ClientId = clientId;
                ClientSecret = clientSecret;
            }
        }

        public class AccessRoleSet : Event
        {
            public readonly Guid BusinessApplicationId;
            public readonly string RoleName;

            public AccessRoleSet(Guid businessApplicationId, string roleName)
            {
                BusinessApplicationId = businessApplicationId;
                RoleName = roleName;
            }
        }

        public class PolicyCreated : Event
        {
            public readonly Guid SecuredApplicationId;
            public readonly Guid PolicyId;
            public readonly string PolicyName;

            public PolicyCreated(Guid securedApplicationId, Guid policyId, string policyName)
            {
                SecuredApplicationId = securedApplicationId;
                PolicyId = policyId;
                PolicyName = policyName;
            }
        }
    }
}