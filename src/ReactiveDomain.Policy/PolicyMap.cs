using Newtonsoft.Json;
using ReactiveDomain.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ReactiveDomain.Policy
{
    public class PolicyMap
    {
        private readonly HashSet<Policy> _policies;
        public readonly Policy DefaultPolicy;
        public readonly string ApplicationName;
        public PolicyMap(Policy defaultPolicy, string applicationName = null)
        {
            Ensure.NotNull(defaultPolicy, nameof(defaultPolicy));
            if (string.IsNullOrWhiteSpace(applicationName)) { applicationName = Assembly.GetEntryAssembly()?.GetName().Name; }
            _policies = new HashSet<Policy>();
            DefaultPolicy = defaultPolicy;
            _policies.Add(DefaultPolicy);
            ApplicationName = applicationName;
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
                policy = DefaultPolicy;
            }
            else
            {
                policy = _policies.FirstOrDefault(p => string.Equals(p.PolicyName, user.PolicyName));
            }
            if (policy == null) { throw new ArgumentOutOfRangeException(nameof(user), $"Provided User PolicyName not found in Application Policy Map"); }

            var roles = new HashSet<Role>();
            if (user.RoleNames != null && user.RoleNames.Any())
            {
                var names = policy.SingleRole ? user.RoleNames.Take(1) : user.RoleNames;
                foreach (var roleName in names)
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
        public PolicyDTO GetPolicyDTO(string policyName, string clientName)
        {
            Ensure.NotNullOrEmpty(policyName, nameof(policyName));
            if (string.IsNullOrWhiteSpace(clientName)) { clientName = ApplicationName; }
            var nameWrapper = new Policy(policyName);
            if (!_policies.Contains(nameWrapper)) { throw new ArgumentOutOfRangeException($"Policy {policyName } not found."); }
            var pol = _policies.First(p => p.Equals(nameWrapper));

            var map = new PolicyDTO()
            {
                ApplicationName = ApplicationName,
                ClientName = clientName,
                PolicyName = pol.PolicyName,
                Roles = pol.RoleNames.ToList()
            };
            return map;
        }
        public void ExportPolicy(PolicyDTO policy, string filePath)
        {
            Ensure.NotNull(policy, nameof(policy));

            using (var fs = new FileInfo(filePath).CreateText())
            using (JsonWriter writer = new JsonTextWriter(fs))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(writer, policy);
            }
        }
    }
}

