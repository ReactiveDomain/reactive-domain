using System.Collections.Generic;

namespace ReactiveDomain.Users.ReadModels
{
    public interface IUserEntitlementRM
    {
        List<Role> RolesForUser(string userId, string userName, string authDomain, Application application);
    }
}