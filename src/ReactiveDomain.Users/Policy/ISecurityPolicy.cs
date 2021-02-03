using System;
using System.Collections.Generic;
using System.Security.Claims;
using ReactiveDomain.Users.ReadModels;

namespace ReactiveDomain.Users.Policy
{
    public interface ISecurityPolicy {
        string ApplicationName { get; }
        string ApplicationVersion { get; }
        bool TrySetCurrentUser(ClaimsPrincipal authenticatedUser, out User user);
        User GetCurrentUser();
    }
}
