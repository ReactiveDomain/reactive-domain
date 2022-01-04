using ReactiveDomain.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ReactiveDomain.Policy
{
    public class PolicyMap
    {
        private readonly HashSet<Policy> _policies;
        private readonly Policy _defaultPolicy;
        public PolicyMap(Policy defaultPolicy)
        {
            Ensure.NotNull(defaultPolicy, nameof(defaultPolicy));
            _policies = new HashSet<Policy>();
            _defaultPolicy = defaultPolicy;
            _policies.Add(_defaultPolicy);
        }
        public void AddAdditionalPolicy(Policy policy)
        {
            _policies.Add(policy);
        }
        public UserPolicy GetUserPolicy(UserDetails user)
        {
            Ensure.NotNull(user, nameof(user));
            Policy policy = null;
            if (string.IsNullOrWhiteSpace(user.PolicyName))
            {
                policy = _defaultPolicy;
            }
            else
            {
                policy = _policies.FirstOrDefault(p => string.Equals(p.PolicyName, user.PolicyName));
            }
            if (policy == null) { throw new ArgumentOutOfRangeException(nameof(user), $"Provided User PolicyName not found in Application Policy Map"); }

            var roles = new HashSet<Role>();
            if (user.RoleNames != null && user.RoleNames.Any())
            {                
                foreach (var roleName in user.RoleNames)
                {
                    if (policy.TryGetRole(roleName, out var role))
                    {
                        roles.Add(role);
                    }
                    else
                    {
                        roles.Add(new Role(roleName)); //custom role 
                    }
                }
            }
            return new UserPolicy(user, roles);
        }
    }
}
