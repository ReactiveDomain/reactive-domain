using System;
using System.Collections.Generic;
using ReactiveDomain.Users.ReadModels;

namespace ReactiveDomain.Users.Policy
{
    public class SecurityPolicy: ISecurityPolicy
    {
        private readonly List<Role> _roles;
        private readonly List<Permission> _permissions;
        public Application App { get; }
        public IReadOnlyList<Role> Roles => _roles.AsReadOnly();
        public IReadOnlyList<Permission> Permissions => _permissions.AsReadOnly();
        public string ApplicationName => App.Name;
        public string ApplicationVersion => App.Version;
        
        public SecurityPolicy(
            Application app,
            List<Role> roles,
            List<Permission> permissions) {
            App = app;
            _roles = roles;
            _permissions = permissions;
        }

        public IReadOnlyList<Role> ListUserRoles(Guid userId) {
            throw new NotImplementedException();
        }

        public bool HasPermission(Guid userId, Permission permission) {
            throw new NotImplementedException();
        }
        //used for synchronizing with the backing store
        internal void AddRole(Role role) {
            _roles.Add(role);
        }
        //used for synchronizing with the backing store
        internal void AddPermission(Permission permission) {
            _permissions.Add(permission);
        }
    }
}
