using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using ReactiveDomain.Users.ReadModels;

namespace ReactiveDomain.Policy.Application
{
    public sealed class SecurityPolicyBuilder : IDisposable
    {
        private readonly string _policyName;
        private readonly SecuredApplication _app;
        private readonly Dictionary<string, Role> _roles = new Dictionary<string, Role>();
        private readonly List<Type> _permissions = new List<Type>(); //hash or similar?
        private bool _disposed;
        private bool _isBuilt;
        private Func<ClaimsPrincipal, UserDTO> _findUserFunc;

        public SecurityPolicyBuilder(string policyName = "default")
        {
            _policyName = policyName;
            _app = new SecuredApplication(
                Guid.Empty,
                Assembly.GetEntryAssembly()?.GetName().Name,              
                Assembly.GetEntryAssembly()?.GetName().Version?.ToString(),
                false);
        }

        public SecurityPolicyBuilder(string appName, Version appVersion, string policyName = "default", bool oneRolePerUser = false)
        {
            _policyName = policyName;
            _app = new SecuredApplication(
                Guid.Empty,
                appName,
                appVersion.ToString(),
                oneRolePerUser);
        }

        public SecurityPolicyBuilder AddRole(string roleName, params Action<RoleBuilder>[] roleActions)
        {
            if (_disposed || _isBuilt) throw new ObjectDisposedException(nameof(SecurityPolicyBuilder));
            var roleBuilder = new RoleBuilder(roleName, this);
            if (roleActions == null) return this;

            foreach (var roleAction in roleActions)
            {
                roleAction(roleBuilder);
            }
            return this;
        }
        
        public SecurityPolicyBuilder WithUserResolver(Func<ClaimsPrincipal, UserDTO> findUserFunc)
        {
            _findUserFunc = findUserFunc;
            return this;
        }


        public class RoleBuilder
        {
            private readonly SecurityPolicyBuilder _policyBuilder;
            private readonly Role _role;
            public RoleBuilder(string name, SecurityPolicyBuilder policyBuilder)
            {
                _policyBuilder = policyBuilder;
               // if (_policyBuilder._roles.ContainsKey(name)) throw new DuplicateRoleException(name, policyBuilder._policyName);
                _role = new Role(Guid.Empty, name, Guid.Empty);
                _policyBuilder._roles.Add(name, _role);
            }


            public RoleBuilder WithCommand(Type t)
            {
                _role.AllowPermission(t);

                return this;
            }
        }

        public SecurityPolicy Build()
        {
            _isBuilt = true;
          
            var policy = new SecurityPolicy(
                                _policyName,
                                Guid.Empty,
                                _app,
                                _roles.Values.ToList());
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
