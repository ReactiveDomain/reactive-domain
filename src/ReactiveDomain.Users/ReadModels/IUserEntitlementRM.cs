using System.Collections.Generic;

namespace ReactiveDomain.Users.ReadModels
{
    public interface IUserEntitlementRM
    {
        List<RoleModel> RolesForUser(string userId, string userName, string authDomain, ApplicationModel application);
    }
}