using System.Collections.Generic;
using System.Security.Claims;
using ReactiveDomain.Foundation;
using ReactiveDomain.Policy.ReadModels;
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
        PolicyUser GetPolicyUserFrom(UserDTO user, IConfiguredConnection conn, List<string> additionalRoles);
        IReadOnlyList<PolicyUser>  PolicyUsers { get; }
    }
}
