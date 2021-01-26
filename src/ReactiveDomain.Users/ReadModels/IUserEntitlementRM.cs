using System;
using System.Collections.Generic;
using ReactiveDomain.Users.Policy;

namespace ReactiveDomain.Users.ReadModels
{
    public interface IUserEntitlementRM
    {
        List<Role> RolesForUser(string userId, string userName, string authDomain, Guid policyId);
    }
}