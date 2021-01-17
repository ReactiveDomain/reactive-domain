using System;
using System.Collections.Generic;
using ReactiveDomain.Users.ReadModels;

namespace ReactiveDomain.Users.Policy
{
    //application security policy read model
    //defined in the reactive domain users read models
    public interface ISecurityPolicy {
        string ApplicationName { get; }
        string ApplicationVersion { get; }
        IReadOnlyList<Role> GetUserRoles(Guid userId);
        IReadOnlyList<Permission> GetUserPermissions(Guid userId);
    }
    //defined in application
    public interface IPolicyDefinition {
        IReadOnlyList<RoleDTO> UserRoles { get; }
        IReadOnlyList<string> Permissions { get; }
        string ApplicationName { get; }
        string ApplicationVersion { get; } // TODO: Use Version instead of string?
    }
    //defined in reactive domain users domain services
    public interface IConfigureSecurity {
        void Configure(IPolicyDefinition definition, IStreamStoreConnection conn);
    }
}
