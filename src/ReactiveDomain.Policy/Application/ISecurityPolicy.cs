using System.Security.Claims;
using ReactiveDomain.Users.Domain;
using ReactiveDomain.Users.ReadModels;

namespace ReactiveDomain.Policy.Application
{
    public interface ISecurityPolicy
    {
        string ApplicationName { get; }
        string ApplicationVersion { get; }
        string ClientId { get; }
        string[] RedirectionUris { get; }
        string ClientSecret { get; }
        bool TrySetCurrentUser(ClaimsPrincipal authenticatedUser, out UserDTO user);
        UserDTO GetCurrentUser();
    }
}
