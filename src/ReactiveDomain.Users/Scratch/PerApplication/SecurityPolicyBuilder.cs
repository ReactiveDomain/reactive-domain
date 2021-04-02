using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace ReactiveDomain.Users.Scratch.PerApplication
{
    public class SecurityPolicyBuilder : IDisposable
    {
        private readonly string _policyName;
        private readonly SecuredApplication _application;
        private readonly Dictionary<string, Role> _roles = new Dictionary<string, Role>();
        private readonly HashSet<Type> _actions = new HashSet<Type>();
        private bool _isBuilt;
        private string _defaultRole;
        private bool _disposed = false;

        public SecurityPolicyBuilder SetDefaultRole(string roleName)
        {
            _defaultRole = roleName;
            return this;
        }

        public SecurityPolicyBuilder BuildRole(string roleName, params Action<RoleBuilder>[] actions)
        {
            if (_disposed || _isBuilt) throw new ObjectDisposedException(nameof(SecurityPolicy));
            
            if (actions != null)
            {
                var builder = new RoleBuilder(roleName, this);

                foreach (var action in actions)
                {
                    action(builder);
                }
            }

            return this;
        }

        public SecurityPolicy Build()
        {
            _isBuilt = true;
            Role defaultRole = default;
            if (_roles.ContainsKey(_defaultRole ?? string.Empty))
            {
                defaultRole = _roles[_defaultRole];
            }
            else if (!string.IsNullOrWhiteSpace(_defaultRole))
            {
                defaultRole = new Role(Guid.Empty, _defaultRole, Guid.Empty);
                _roles.Add(_defaultRole, defaultRole);
            }

            var policy = new SecurityPolicy(
                Guid.Empty, 
                _policyName,
                _application,
                defaultRole,
                _roles.Values.AsEnumerable());
            
            Dispose();

            return policy;
        }

        public class RoleBuilder
        {
            private readonly SecurityPolicyBuilder _policyBuilder;
            private readonly Role _role;
            
            public RoleBuilder(string roleName, SecurityPolicyBuilder policyBuilder)
            {
                _policyBuilder = policyBuilder;
                if (_policyBuilder._roles.ContainsKey(roleName))
                    throw new Exception(
                        $"{roleName} already exists within {policyBuilder._policyName}.  Role names must be unique");

                _role = new Role(Guid.Empty, roleName, Guid.Empty);
                _policyBuilder._roles.Add(roleName, _role);
            }

            public RoleBuilder WithAction(Type t)
            {
                _role.AllowAction(t);
                return this;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _disposed = true;
            _roles?.Clear();
            _actions?.Clear();
        }
    }
}