using System;
using System.Collections.Generic;
using ReactiveDomain.Foundation;
using ReactiveDomain.Users.ReadModels;

namespace ReactiveDomain.Policy.Application
{
    public interface ISecurityPolicy
    {
        string ApplicationName { get; }
        string SecurityModelVersion { get; }
        string ClientId { get; }
        string[] RedirectionUris { get; }
        string ClientSecret { get; }
        PolicyUser GetPolicyUserFrom(Guid policyUserId, UserDTO user, IConfiguredConnection conn, List<string> additionalRoles);
        IReadOnlyList<PolicyUser>  PolicyUsers { get; }
    }
}
