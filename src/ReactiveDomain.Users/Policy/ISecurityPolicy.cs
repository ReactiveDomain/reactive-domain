using System;
using System.Collections.Generic;
using EventStore.ClientAPI;
using ReactiveDomain.Users.ReadModels;

namespace ReactiveDomain.Users.Policy
{
    public interface ISecurityPolicy {
        public  string ApplicationName { get; }
        public  string ApplicationVersion { get;  }
        public  IReadOnlyList<Role> GetUserRoles(Guid userId);
        public  IReadOnlyList<Permission> GetUserPermissions(Guid userId);
        public  void ConfigurePolicy(IEventStoreConnection conn);
    }
}
