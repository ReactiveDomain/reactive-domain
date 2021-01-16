using System;
using System.Collections.Generic;
using EventStore.ClientAPI;
using ReactiveDomain.Users.ReadModels;

namespace ReactiveDomain.Users.Policy
{
    //application security policy read model
    //defined in the reactive domain users read models
    public interface ISecurityPolicy {
        string ApplicationName { get; }
        string ApplicationVersion { get;  }
        IReadOnlyList<Role> GetUserRoles(Guid userId);
        IReadOnlyList<Permission> GetUserPermissions(Guid userId);
    }
    //defined in application
    public interface IPolicyDefinition {
        //todo: list of roles
        //todo: list of permissions
        //todo: role hierarchy definition
        //todo: role => permission mapping
        //todo: application name
        //todo: application version
    }
    //defined in reactive domain users domain services
    public interface IConfigureSecurity {
        void Configure(IPolicyDefinition definition, IEventStoreConnection conn);
    }
}
