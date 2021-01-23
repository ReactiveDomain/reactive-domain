using System;
using System.Collections.Generic;
using ReactiveDomain.Users.ReadModels;

namespace ReactiveDomain.Users.Policy
{
    //application security policy read model
    //defined in the bootstrap and enriched from the reactive domain users read models
    public interface ISecurityPolicy {
        string ApplicationName { get; }
        string ApplicationVersion { get; }
        IReadOnlyList<Role> ListUserRoles(Guid userId);
        bool HasPermission(Guid userId, Permission permission);
    }
}
