using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using ReactiveDomain.Users.Domain;
using ReactiveDomain.Users.ReadModels;

namespace ReactiveDomain.Users.Policy
{
    public sealed class SecurityPolicyBuilder:IDisposable
    {
        private readonly Application _app;
        private readonly Dictionary<string, Role> _roles = new Dictionary<string, Role>();
        private readonly Dictionary<string, Permission> _permissions = new Dictionary<string, Permission>();
        private bool _diposed;
        private bool _isBuilt;

        public SecurityPolicyBuilder()
        {
            _app = new Application(
                Guid.Empty,
                Assembly.GetExecutingAssembly().GetName().Name,
                Assembly.GetExecutingAssembly().GetName().Version.ToString());
        }

        public SecurityPolicyBuilder(string appName, Version appVersion)
        {
            _app = new Application(
                Guid.Empty,
                appName,
                appVersion.ToString());
        }

        public SecurityPolicyBuilder AddRole(string roleName, params Action<RoleBuilder>[] roleActions)
        {
            if(_diposed || _isBuilt) throw new ObjectDisposedException(nameof(SecurityPolicyBuilder));
            var roleBuilder = new RoleBuilder(roleName, _app, this);
            if (roleActions == null) return this;

            foreach (var roleAction in roleActions)
            {
                roleAction(roleBuilder);
            }
            return this;
        }


        public class RoleBuilder
        {
            private readonly Application _app;
            private readonly SecurityPolicyBuilder _policyBuilder;
            private readonly Role _role;
            public RoleBuilder(string name, Application app, SecurityPolicyBuilder policyBuilder)
            {
                _app = app;
                _policyBuilder = policyBuilder;
                if (_policyBuilder._roles.ContainsKey(name)) throw new DuplicateRoleException(name, _app.Name);
                _role = new Role(Guid.Empty, name, app);
                _policyBuilder._roles.Add(name, _role);
            }

            public RoleBuilder WithChildRole(string roleName, params Action<RoleBuilder>[] roleActions) {
                var roleBuilder = new RoleBuilder(roleName, _app, _policyBuilder);
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
                if (!_policyBuilder._permissions.TryGetValue(permissionName, out var permission)) {
                    permission = new Permission(
                        Guid.Empty,
                        permissionName,
                        _app);
                    _policyBuilder._permissions.Add(permissionName, permission);
                }
                _role.AddPermission(permission);
                return this;
            }
        }

        public SecurityPolicy Build() {
            _isBuilt = true;
            var policy = new SecurityPolicy(_app,_roles.Values.ToList(),_permissions.Values.ToList());
            Dispose();
            return policy;
        }

        public void Dispose() {
            _diposed = true;
            _roles.Clear();
            _permissions.Clear();
        }
    }


}
