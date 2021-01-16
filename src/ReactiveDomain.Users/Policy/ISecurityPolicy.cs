using System;
using System.Collections.Generic;
using EventStore.ClientAPI;
using ReactiveDomain.Users.ReadModels;

namespace ReactiveDomain.Users.Policy
{
    public interface ISecurityPolicy {
        string ApplicationName { get; }
        string ApplicationVersion { get;  }
        IReadOnlyList<Role> GetUserRoles(Guid userId);
        IReadOnlyList<Permission> GetUserPermissions(Guid userId);
        void ConfigurePolicy(IEventStoreConnection conn);
    }
}
