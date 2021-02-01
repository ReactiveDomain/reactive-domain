using System;
using System.Collections.Generic;

namespace ReactiveDomain.Users.Policy
{
    public class SecurityPolicy: ISecurityPolicy {
        public readonly string PolicyName;
        public  Guid PolicyId { get; internal set; }
        private readonly List<Role> _roles;
        private readonly List<Permission> _permissions;
        
        public Application OwningApplication { get; }
        public IReadOnlyList<Role> Roles => _roles.AsReadOnly();
        public IReadOnlyList<Permission> Permissions => _permissions.AsReadOnly();
        public string ApplicationName => OwningApplication.Name;
        public string ApplicationVersion => OwningApplication.Version;
        
        public SecurityPolicy(
            string policyName,
            Guid policyId,
            Application owningApplication,
            List<Role> roles = null,
            List<Permission> permissions = null) {
            PolicyName = policyName;
            PolicyId = policyId;
            OwningApplication = owningApplication;
            _roles = roles ?? new List<Role>();
            _permissions = permissions ?? new List<Permission>();
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
