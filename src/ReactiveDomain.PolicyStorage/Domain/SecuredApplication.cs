using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ReactiveDomain.IdentityStorage.Domain;
using ReactiveDomain.Messaging;
using ReactiveDomain.Policy.Messages;
using ReactiveDomain.Util;
using static ReactiveDomain.Policy.Messages.ApplicationMsgs;

[assembly: InternalsVisibleTo("ReactiveDomain.IdentityStorage")]
namespace ReactiveDomain.Policy.Domain
{
    /// <summary>
    /// Aggregate for a Application.
    /// </summary>

    internal class SecuredApplication : AggregateRoot
    {
        private readonly Dictionary<Guid, SecurityPolicy> _policies = new Dictionary<Guid, SecurityPolicy>();
        private readonly HashSet<string> _policyNames = new HashSet<string>();
        private HashSet<Guid> _clientRegistrations = new HashSet<Guid>();
        private string _clientPrefix;
        public bool OneRolePerUser;
        // ReSharper disable once UnusedMember.Local
        // used via reflection in the repository
        private SecuredApplication()
        {
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            Register<ApplicationCreated>(Apply);
            Register<PolicyCreated>(Apply);
            Register<ClientRegistrationAdded>(Apply);
        }
        //Apply State Changes
        private void Apply(ApplicationCreated @event)
        {

            Id = @event.ApplicationId;
            _clientPrefix = @event.Name;
            OneRolePerUser = @event.OneRolePerUser;
        }

        private void Apply(PolicyCreated @event)
        {
            var policy = new SecurityPolicy(@event.PolicyId, @event.ClientId, this);           
            if (DefaultPolicy == null) { DefaultPolicy = policy; }
            _policies.Add(@event.PolicyId, policy);
            _policyNames.Add(@event.ClientId);
        }

        private void Apply(ClientRegistrationAdded @event)
        {
            _clientRegistrations.Add(@event.ClientId);
        }
        //Public Methods

        /// <summary>
        /// Create a new Application.
        /// </summary>
        public SecuredApplication(
            Guid id,
            Guid defaultPolicyId,
            string defaultClientId,
            string version,
            bool oneRolePerUser,
            ICorrelatedMessage source)
            : base(source)
        {
            Ensure.NotEmptyGuid(id, nameof(id));
            Ensure.NotEmptyGuid(defaultPolicyId, nameof(defaultPolicyId));
            Ensure.NotNullOrEmpty(defaultClientId, nameof(defaultClientId));
            Ensure.NotNullOrEmpty(version, nameof(version));
            Ensure.NotEmptyGuid(source.CorrelationId, nameof(source));
            RegisterEvents();
            Raise(new ApplicationCreated(
                         id,
                         defaultClientId,
                         version,
                         oneRolePerUser));
            Raise(new PolicyCreated(defaultPolicyId, defaultClientId, id, oneRolePerUser));
        }

        /// <summary>
        /// Retire an application that is no longer in use.
        /// </summary>
        public void Retire()
        {
            // Event should be idempotent in RMs, so no validation necessary.
            Raise(new ApplicationRetired(Id));
        }

        /// <summary>
        /// Re-activate a retired application that is being put back into use.
        /// </summary>
        public void Unretire()
        {
            // Event should be idempotent in RMs, so no validation necessary.
            Raise(new ApplicationUnretired(Id));
        }
        public SecurityPolicy DefaultPolicy { get; private set; }        
        public IReadOnlyList<SecurityPolicy> Policies => _policies.Values.ToList().AsReadOnly();
        public SecurityPolicy AddAdditionalPolicy(Guid policyId, string policyName)
        {
            Ensure.NotEmptyGuid(policyId, nameof(policyId));
            Ensure.NotNullOrEmpty(policyName, nameof(policyName));
            if (_policies.ContainsKey(policyId) || _policyNames.Contains(policyName))
            {
                throw new InvalidOperationException($"Cannot add duplicate Policy: {{ Name:{policyName}, Id:{policyId} }}");
            }
            Raise(new ApplicationMsgs.PolicyCreated(policyId, policyName, Id, OneRolePerUser));
            return _policies[policyId];
        }
        public void AddClientRegistration(Client client)
        {
            Ensure.True(() => client.ClientName.StartsWith(_clientPrefix, StringComparison.OrdinalIgnoreCase), "Client name mismatch");
            if (!_clientRegistrations.Contains(client.Id))
            {
                Raise(new ClientRegistrationAdded(client.Id, Id));
            }
        }
    }
}
