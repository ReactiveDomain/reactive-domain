using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using ReactiveDomain.Policy.ReadModels;
using ReactiveDomain.Users.Domain;
using ReactiveDomain.Users.ReadModels;

namespace ReactiveDomain.Policy.Application
{
    public class SecurityPolicy : ISecurityPolicy
    {
        public readonly string PolicyName;
        public Guid PolicyId { get; internal set; }
        private readonly List<Role> _roles;
        private readonly List<UserDTO> _users = new List<UserDTO>();
        private Func<ClaimsPrincipal, UserDTO> _findUser;

        public SecuredApplication OwningApplication { get; }
        public IReadOnlyList<Role> Roles => _roles.AsReadOnly();
        public IReadOnlyList<UserDTO> Users => _users.AsReadOnly();
        public string ApplicationName => OwningApplication.Name;
        public string ApplicationVersion => OwningApplication.Version;

        public string ClientId => OwningApplication.Name;
        public string[] RedirectionUris => OwningApplication.RedirectionUris;
        public string ClientSecret => OwningApplication.ClientSecret;

        public AuthorizedUser CurrentUser { get; set; }

        public bool TrySetCurrentUser(ClaimsPrincipal authenticatedUser, out User user)
        {
            throw new NotImplementedException();
            //todo:case no users at all - debug auto start
            //if (!_users.Any()) {
            //    user = new User(Guid.NewGuid(), authenticatedUser.Identity.Name,authenticatedUser.Identity.);
            //    AddUser();
            //    return true;
            //}
            //todo: check if user is authorized
        }

        public bool TrySetCurrentUser(ClaimsPrincipal authenticatedUser, out UserDTO user) {
            throw new NotImplementedException();
        }

        UserDTO ISecurityPolicy.GetCurrentUser() {
            throw new NotImplementedException();
        }

        public User GetCurrentUser()
        {
            throw new NotImplementedException();
        }

        public bool HasUsers()
        {
            return _users.Any();
        }

        public SecurityPolicy(
            string policyName,
            Guid policyId,
            SecuredApplication owningApplication,
            Func<ClaimsPrincipal, UserDTO> findUser,
            List<Role> roles = null)
        {
            PolicyName = policyName;
            PolicyId = policyId;
            OwningApplication = owningApplication;
            _findUser = findUser;
            _roles = roles ?? new List<Role>();
        }

        //used for synchronizing with the backing store
        internal void AddRole(Role role)
        {
            _roles.Add(role);
        }
        //used for synchronizing with the backing store
        internal void AddUser(UserDTO user)
        {
            _users.Add(user);
        }

        public UserDTO ResolveUser(ClaimsPrincipal principal) => _findUser(principal);

        public IReadOnlyList<Type> GetEffectivePermissions(IEnumerable<Role> roles)
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
