using ReactiveDomain.Users.Policy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace ReactiveDomain.Users.PolicyModel
{
    public class SecurityPolicy : ISecurityPolicy
    {
        public readonly string PolicyName;
        public Guid PolicyId { get; internal set; }
        private readonly List<Role> _roles;
        private readonly Role _defaultRole;
        private readonly List<User> _users = new List<User>();
        private AuthorizedUser _currentUser;
        private Func<ClaimsPrincipal, User> _findUser;

        public SecuredApplication OwningApplication { get; }
        public IReadOnlyList<Role> Roles => _roles.AsReadOnly();
        public IReadOnlyList<User> Users => _users.AsReadOnly();
        public string ApplicationName => OwningApplication.Name;
        public string ApplicationVersion => OwningApplication.Version;

        public AuthorizedUser CurrentUser
        {
            get => _currentUser;
            set { _currentUser = value; }
        }

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
            Func<ClaimsPrincipal, User> findUser,
            List<Role> roles = null,
            Role defaultRole = null)
        {
            PolicyName = policyName;
            PolicyId = policyId;
            OwningApplication = owningApplication;
            _findUser = findUser;
            _roles = roles ?? new List<Role>();
            _defaultRole = defaultRole;
            if (_defaultRole != null && !_roles.Any(r => r.Name.Equals(_defaultRole.Name)))
            {
                _roles.Add(_defaultRole);
            }
        }

        //used for synchronizing with the backing store
        internal void AddRole(Role role)
        {
            _roles.Add(role);
        }
        //used for synchronizing with the backing store
        internal void AddUser(User user)
        {
            _users.Add(user);
        }

        public User ResolveUser(ClaimsPrincipal principal) => _findUser(principal);

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
