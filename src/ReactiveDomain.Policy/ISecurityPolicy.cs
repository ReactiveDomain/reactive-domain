using ReactiveDomain.Users.Policy;
using System.Security.Claims;

namespace ReactiveDomain.Users.PolicyModel
{
    public interface ISecurityPolicy
    {
        string ApplicationName { get; }
        string ApplicationVersion { get; }
        bool TrySetCurrentUser(ClaimsPrincipal authenticatedUser, out User user);
        User GetCurrentUser();
    }
}
