using System.Security.Claims;

namespace ReactiveDomain.Users.Policy
{
    public interface ISecurityPolicy {
        string ApplicationName { get; }
        string ApplicationVersion { get; }
        bool TrySetCurrentUser(ClaimsPrincipal authenticatedUser, out User user);
        User GetCurrentUser();
    }
}
