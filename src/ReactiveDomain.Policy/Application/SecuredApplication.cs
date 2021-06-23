using System;
using System.Collections.Generic;
using System.Linq;

namespace ReactiveDomain.Policy.Application
{

    public class SecuredApplication
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public string Version { get; private set; }
        public bool OneRolePerUser { get; private set; }
        public string[] RedirectionUris { get; set; }
        public string ClientSecret { get; set; }

        private readonly List<SecurityPolicy> _polices;
        public IReadOnlyList<SecurityPolicy> Policies => _polices.AsReadOnly();

        public SecuredApplication(
            Guid id,
            string name,
            string version,
            bool oneRolePerUser,
            IEnumerable<SecurityPolicy> policies = null)
        {
            Id = id;
            Name = name;
            Version = version;
            OneRolePerUser = oneRolePerUser;
            _polices = policies?.ToList() ?? new List<SecurityPolicy>();
        }
        /// <summary>
        /// Used when syncing with the backing db
        /// </summary>
        /// <param name="id"></param>
        /// <param name="name"></param>
        /// <param name="version"></param>
        internal void UpdateApplicationDetails(
            Guid? id,
            string name = null,
            string version = null)
        {
            if (id.HasValue && id.Value != Guid.Empty) { Id = id.Value; }
            if (!string.IsNullOrWhiteSpace(name)) { Name = name; }
            if (!string.IsNullOrWhiteSpace(version)) { Version = version; }
        }

        public void AddPolicy(SecurityPolicy policy)
        {
            //don't add duplicates, but the ID might be Guid.Empty if db sync hasn't happened
            var existingPolicy = _polices.FirstOrDefault(p =>
                p.PolicyId == policy.PolicyId || p.PolicyName.Equals(policy.PolicyName));
            if (existingPolicy == null)
            {
                _polices.Add(policy);
            }
            //return idempotent success
        }
    }
}