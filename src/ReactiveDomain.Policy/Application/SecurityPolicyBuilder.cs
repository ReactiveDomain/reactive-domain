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

        public SecurityPolicyBuilder(string appName, Version securityModelVersion, string policyName = "default", bool oneRolePerUser = false)
        {
            _policyName = policyName;
            _app = new SecuredApplication(
                Guid.Empty,
                appName,
                securityModelVersion.ToString(),
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

        public class ClientBuilder
        {
            string[] _redirectionUris = null;
            public ClientBuilder WithRedirectionUris(params string[] uris)
            {
                _redirectionUris = uris;
                return this;
            }
            string[] _postLogoutUris = null;
            public ClientBuilder WithPostLogoutUris(params string[] uris)
            {
                _postLogoutUris = uris;
                return this;
            }
            string _fontChannelLogoutUri = null;
            public ClientBuilder WithFrontChannelLogoutUri(string fontChannelLogoutUri)
            {
                _fontChannelLogoutUri = fontChannelLogoutUri;
                return this;
            }
        }
        public class RoleBuilder
        {
            private readonly SecurityPolicyBuilder _policyBuilder;
            private readonly Role _role;
            public RoleBuilder(string name, SecurityPolicyBuilder policyBuilder)
            {
                _policyBuilder = policyBuilder;
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
            throw new NotImplementedException();
            //_isBuilt = true;
            //var client = new Identity.Domain.Client(
            //    Guid.NewGuid(),

            //    );
            //Guid id,
            //    Guid applicationId,
            //    string clientName, //todo: value object?
            //    string encryptedClientSecret,
            //    string[] redirectUris,
            //    string[] logoutRedirectUris,
            //    string frontChannlLogoutUri,
            //    ICorrelatedMessage source)
            //throw new NotImplementedException();
            //var policy = new SecurityPolicy(
            //                    _policyName,
            //                    Guid.Empty,
            //                    _app,
            //                    client,
            //                    _roles.Values.ToList());
            //Dispose();
            //return policy;
        }

        public void Dispose()
        {
            _disposed = true;
            _roles.Clear();
            _permissions.Clear();
        }
    }


}
