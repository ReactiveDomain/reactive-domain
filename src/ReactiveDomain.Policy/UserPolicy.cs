using ReactiveDomain.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveDomain.IdentityStorage.ReadModels;

namespace ReactiveDomain.Policy
{
    public class UserPolicy
    {
        public static UserPolicy EmptyPolicy()
        {
            return new UserPolicy();
        }
        private UserPolicy()
        {           
            UserId = Guid.Empty;
        }
        public UserDTO User { get; }
        public Guid UserId { get; }
        public IReadOnlyList<Role> Roles => _roles.ToList().AsReadOnly();
        private readonly HashSet<Role> _roles = new HashSet<Role>();
        private readonly HashSet<string> _roleNames = new HashSet<string>();
        private readonly HashSet<Permission> _permissions = new HashSet<Permission>();
        private readonly HashSet<Type> _permissionTypes = new HashSet<Type>();
        private readonly HashSet<string> _permissionNames = new HashSet<string>();

        public UserPolicy(UserDTO user, HashSet<Role> grantedRoles)
        {
            Ensure.NotEmptyGuid(user.UserId, nameof(user));
            Ensure.NotNull(user, nameof(user));
            Ensure.NotNull(grantedRoles, nameof(grantedRoles));
            User = user;
            UserId = user.UserId;
            foreach (var role in grantedRoles)
            {
                AddRole(role);
            }
        }

        public void AddRole(Role role)
        {
            _roles.Add(role);
            _roleNames.Add(role.RoleName.Trim().ToLowerInvariant());
            foreach (var permission in role.Permissions)
            {
                _permissions.Add(permission);
                _permissionNames.Add(permission.PermissionName);
                if (permission.TryResovleType())
                {
                    _permissionTypes.Add(permission.PermissionType);
                }
            }
        }

        public bool HasRole(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName)) { return false; }
            return _roleNames.Contains(roleName.Trim().ToLowerInvariant());
        }
        public bool HasRole(Role role)
        {
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
        public bool HasPermission(Type permissionType)
        {
            if (permissionType == null) { return false; }
            return _permissionTypes.Contains(permissionType);
        }
    }
}
