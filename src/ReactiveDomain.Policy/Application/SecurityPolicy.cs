using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveDomain.Foundation;
using ReactiveDomain.Users.ReadModels;

namespace ReactiveDomain.Policy.Application
{
    public class SecurityPolicy : ISecurityPolicy
    {
        public readonly string PolicyName;
        public Guid PolicyId { get; internal set; }
        private readonly List<Role> _roles;
        private readonly List<PolicyUser> _policyUsers = new List<PolicyUser>();
        public IReadOnlyList<PolicyUser> PolicyUsers => _policyUsers.AsReadOnly();
        private Guid _currentUser;
        public PolicyUser CurrentUser { get; private set; }
        public SecuredApplication OwningApplication { get; }
        public IReadOnlyList<Role> Roles => _roles.AsReadOnly();

        public string ApplicationName => OwningApplication.Name;
        public string ApplicationVersion => OwningApplication.Version;
        public bool OneRolePerUser => OwningApplication.OneRolePerUser;
        public string ClientId => OwningApplication.Name;
        public string[] RedirectionUris => OwningApplication.RedirectionUris;
        public string ClientSecret => OwningApplication.ClientSecret;

        public SecurityPolicy(
            string policyName,
            Guid policyId,
            SecuredApplication owningApplication,
            List<Role> roles = null)
        {
            PolicyName = policyName;
            PolicyId = policyId;
            OwningApplication = owningApplication;
            _roles = roles ?? new List<Role>();
        }

        //used for synchronizing with the backing store
        internal void AddRole(Role role)
        {
            _roles.Add(role);
        }
        public void AddOrUpdateUser(PolicyUser user)
        {
            lock (_policyUsers)
            {
                bool updated = false;
                for (var index = 0; index < PolicyUsers.Count; index++)
                {
                    if (PolicyUsers[index].User.UserId != user.User.UserId) continue;
                    _policyUsers[index] = user;
                    updated = true;
                    break;
                }
                if (!updated)
                {
                    _policyUsers.Add(user);
                }
            }
            TrySetCurrentUser(_currentUser);
        }

        public void RemoveUser(Guid userId)
        {
            bool removed = false;
            lock (_policyUsers)
            {
                for (var index = 0; index < PolicyUsers.Count; index++)
                {
                    if (PolicyUsers[index].User.UserId != userId) continue;
                    var user = _policyUsers[index];
                    _policyUsers.Remove(user);
                    removed = true;
                    break;
                }
            }

            if (removed)
            {
                TrySetCurrentUser(_currentUser);
            }
        }
        /// <summary>
        /// This will attempt to set the the current user by Id from the internal list of authorized Policy Users 
        /// </summary>
        /// <param name="userId">The user Id to look up</param>
        /// <returns><see langword="true"/>if the requested user was found in the list of authorized users</returns>
        public bool TrySetCurrentUser(Guid userId)
        {
            lock (_policyUsers)
            {
                _currentUser = userId;
                CurrentUser = _policyUsers.FirstOrDefault(u => u.User.UserId == _currentUser);
            }
            return CurrentUser != null;
        }
        public PolicyUser GetPolicyUserFrom(UserDTO user, IConfiguredConnection conn, List<string> additionalRoles)
        {
            var repo = conn.GetRepository();
            if (!repo.TryGetById<Domain.PolicyUser>(user.UserId, out var policyUser))
            {
                policyUser = new Domain.PolicyUser(
                    Guid.NewGuid(),
                    PolicyId,
                    user.UserId,
                    new SecurityPolicySyncService.CorrelationSource { CorrelationId = Guid.NewGuid() });
                repo.Save(policyUser);                
            }
            var roleNames = policyUser?.Roles ?? new HashSet<string>();
            roleNames.UnionWith(additionalRoles);
            var roles = roleNames.Select(roleName => _roles.FirstOrDefault(r => r.Name == roleName)).Where(x => x != null).ToList();
            var permissions = GetEffectivePermissions(roles);
            return new PolicyUser(user, roles, permissions);
        }

        private IReadOnlyList<Type> GetEffectivePermissions(IEnumerable<Role> roles)
        {
            HashSet<Type> permissions = new HashSet<Type>();

            foreach (var role in roles)
            {
                permissions.UnionWith(role.AllowedPermissions);
            }

            // figure out effective permissions based on provided roles.
            return permissions.ToList().AsReadOnly();
        }
    }
}
