using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

using IdentityModel;

namespace ReactiveDomain.Users.Scratch.PerApplication
{
    /// <summary>
    /// dealproc: Note - This feels like a read model, and should be listening to information from `User` and `SecuredApplication`
    /// to populate its needs.
    /// </summary>
    public class SecurityPolicy
    {
        private readonly HashSet<Role> _roles = new HashSet<Role>();
        private readonly HashSet<User> _users = new HashSet<User>();
        
        public Guid PolicyId { get; }
        public string PolicyName { get; }
        public SecuredApplication Application { get; }
        public string ApplicationName => Application?.Name ?? string.Empty;
        public string ApplicationVersion => Application?.Version ?? string.Empty;

        public SecurityPolicy(Guid policyId, string policyName, SecuredApplication application, Role defaultRole = null, IEnumerable<Role> roles = null)
        {
            PolicyId = policyId;
            PolicyName = policyName;
            Application = application;
            _roles.UnionWith(roles ?? Enumerable.Empty<Role>());
            if (defaultRole != null && _roles.All(r => !r.Name.Equals(defaultRole.Name)))
            {
                _roles.Add(defaultRole);
            }
        }

        /// <summary>
        /// Resolves a known definition of a known user, given their claimsprincipal.
        /// <remarks>The claim that is used as the key from the ClaimsPrincipal is `sub`, or from IdentityModel, the `JwtClaimTypes.Subject` value.</remarks>
        /// </summary>
        /// <param name="principal"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public bool FindUser(ClaimsPrincipal principal, out User user)
        {
            var subjectId = principal.FindFirst(JwtClaimTypes.Subject).Value;

            if (string.IsNullOrWhiteSpace(subjectId))
                throw new Exception($"'{JwtClaimTypes.Subject}' claim is missing from ClaimsPrincipal.");

            user = _users.SingleOrDefault(u => u.SubjectId == subjectId);
            return user != null;
        }

        /// <summary>
        /// For a given user, examines their list of roles and produces a list of allowed actions they may take
        /// within the system.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public IReadOnlyList<Type> GetEffectiveActions(User user)
        {
            HashSet<Type> permissions = new HashSet<Type>();
            foreach(var role in user.Roles) permissions.UnionWith(role.AllowedActions);
            return permissions.ToList().AsReadOnly();
        }
    }
}