using ReactiveDomain.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ReactiveDomain.Policy
{
    public class UserPolicy
    {
        public UserDetails User { get; }
        public Guid UserId { get; }
        public IReadOnlyList<Role> Roles { get; }
        private readonly HashSet<Role> _roles;
        private readonly HashSet<string> _roleNames = new HashSet<string>();
        private readonly HashSet<Permission> _permissions = new HashSet<Permission>();
        private readonly HashSet<Type> _permissionTypes = new HashSet<Type>();
        private readonly HashSet<string> _permissionNames = new HashSet<string>();

        internal UserPolicy(UserDetails user, HashSet<Role> roles) {
            Ensure.NotEmptyGuid(user.UserId, nameof(user));
            Ensure.NotNull(user, nameof(user));
            Ensure.NotNull(roles, nameof(roles));
            User = user;
            UserId = user.UserId;
            _roles = roles;
            Roles = _roles.ToList().AsReadOnly();
            foreach (var role in roles)
            {
                _roleNames.Add(role.RoleName);
                foreach (var permission in role.Permissions)
                {
                    _permissions.Add(permission);
                    _permissionNames.Add(permission.PermissionName);
                    if (permission.TryResovleType()) {
                        _permissionTypes.Add(permission.PermissionType);
                    }
                }
            }
        }
        public bool HasRole(string roleName) {
            if (string.IsNullOrWhiteSpace(roleName)) { return false; }          
            return _roleNames.Contains(roleName);
        }
        public bool HasRole(Role role) {
            if (role == null) { return false; }
            return _roles.Contains(role);
        }
        public bool HasPermission(string permissionName)
        {
            if (string.IsNullOrWhiteSpace(permissionName)) { return false; }          
            return _permissionNames.Contains(permissionName);
        }
        public bool HasPermission(Permission permission)
        {
            if (permission == null) { return false; }
            return _permissions.Contains(permission);
        }
        public bool  HasPermission(Type permissionType) {
            if (permissionType == null) { return false; }  
            return _permissionTypes.Contains(permissionType);
        }
    }
}
