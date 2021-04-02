using System;
using System.Collections.Generic;
using System.Linq;

namespace ReactiveDomain.Users.Scratch.PerApplication
{
    public class SecuredApplication
    {
        private readonly List<SecurityPolicy> _policies;
        
        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public string Version { get; private set; }
        public IReadOnlyList<SecurityPolicy> Policies => _policies.AsReadOnly();

        public SecuredApplication(Guid id, string name, string version, IEnumerable<SecurityPolicy> policies = null)
        {
            Id = id;
            Name = name;
            Version = version;
            _policies = policies?.ToList() ?? new List<SecurityPolicy>();
        }

        /// <summary>
        /// Adds a policy into this application's definition.
        /// </summary>
        /// <param name="policy"></param>
        public void AddPolicy(SecurityPolicy policy)
        {
            var existing = _policies.FirstOrDefault(p => p.PolicyId == policy.PolicyId || p.PolicyName.Equals(policy.PolicyName));
            if (existing == null)
            {
                _policies.Add(policy);
            }
        }
    }
}