using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ReactiveDomain.Users.Domain;

namespace ReactiveDomain.Users.Policy
{
    public sealed class SecurityPolicyBuilder : IDisposable
    {
        private readonly string _policyName;
        private readonly SecuredApplication _app;
        private readonly Dictionary<string, Role> _roles = new Dictionary<string, Role>();
        private readonly Dictionary<string, Permission> _permissions = new Dictionary<string, Permission>();
        private bool _disposed;
        private bool _isBuilt;
        private string _defaultRole;

        public SecurityPolicyBuilder(string policyName = "default")
        {
            _policyName = policyName;
            _app = new SecuredApplication(
                Guid.Empty,
                Assembly.GetEntryAssembly()?.GetName().Name,
                Assembly.GetEntryAssembly()?.GetName().Version.ToString());
        }

        public SecurityPolicyBuilder(string appName, Version appVersion, string policyName = "default")
        {
            _policyName = policyName;
            _app = new SecuredApplication(
                Guid.Empty,
                appName,
                appVersion.ToString());
        }

        public SecurityPolicyBuilder AddRole(string roleName, params Action<RoleBuilder>[] roleActions)
        {
            if (_disposed || _isBuilt) throw new ObjectDisposedException(nameof(SecurityPolicyBuilder));
            var roleBuilder = new RoleBuilder(roleName,  this);
            if (roleActions == null) return this;

            foreach (var roleAction in roleActions)
            {
                roleAction(roleBuilder);
            }
            return this;
        }

        public SecurityPolicyBuilder WithDefaultRole(string roleName) {
            _defaultRole = roleName;
            return this;
        }


        public class RoleBuilder
        {
            private readonly SecurityPolicyBuilder _policyBuilder;
            private readonly Role _role;
            public RoleBuilder(string name,  SecurityPolicyBuilder policyBuilder)
            {
                _policyBuilder = policyBuilder;
                if (_policyBuilder._roles.ContainsKey(name)) throw new DuplicateRoleException(name, policyBuilder._policyName);
                _role = new Role(Guid.Empty, name, Guid.Empty);
                _policyBuilder._roles.Add(name, _role);
            }

            public RoleBuilder WithChildRole(string roleName, params Action<RoleBuilder>[] roleActions)
            {
                var roleBuilder = new RoleBuilder(roleName,  _policyBuilder);
                _role.AddChildRole(roleBuilder._role);

                if (roleActions == null) return this;

                foreach (var roleAction in roleActions)
                {
                    roleAction(roleBuilder);
                }
                return this;
            }

            public RoleBuilder WithPermission(string permissionName)
            {
                if (!_policyBuilder._permissions.TryGetValue(permissionName, out var permission))
                {
                    permission = new Permission(
                        Guid.Empty,
                        permissionName,
                        Guid.Empty);
                    _policyBuilder._permissions.Add(permissionName, permission);
                }
                _role.AddPermission(permission);
                return this;
            }
        }

        public SecurityPolicy Build()
        {
            _isBuilt = true;
            Role defaultRole = null;
            if (_roles.ContainsKey(_defaultRole)) {
                defaultRole = _roles[_defaultRole];
            }
            else if(!string.IsNullOrWhiteSpace(_defaultRole)){
                defaultRole = new Role(Guid.Empty, _defaultRole, Guid.Empty);
                _roles.Add(_defaultRole, defaultRole);
            }

            var policy = new SecurityPolicy(
                                _policyName, 
                                Guid.Empty, 
                                _app, 
                                _roles.Values.ToList(), 
                                defaultRole, 
                                _permissions.Values.ToList());
            Dispose();
            return policy;
        }

        public void Dispose()
        {
            _disposed = true;
            _roles.Clear();
            _permissions.Clear();
        }
    }


}
