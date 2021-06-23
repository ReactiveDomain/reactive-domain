using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ReactiveDomain.Messaging;
using ReactiveDomain.Policy.Messages;
using ReactiveDomain.Util;

[assembly: InternalsVisibleTo("ReactiveDomain.Identity")]
namespace ReactiveDomain.Policy.Domain
{
    /// <summary>
    /// Aggregate for a Application.
    /// </summary>
   
    internal class SecuredApplication : AggregateRoot
    {
        private readonly Dictionary<Guid, SecurityPolicy> _policies = new Dictionary<Guid, SecurityPolicy>();
        private readonly HashSet<string> _policyNames = new HashSet<string>();
        private string _clientId;
        private bool _stsClientDetailsConfigured;
        private bool _oneRolePerUser;
        // ReSharper disable once UnusedMember.Local
        // used via reflection in the repository
        private SecuredApplication()
        {
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            Register<ApplicationMsgs.ApplicationCreated>(Apply);
            Register<ApplicationMsgs.PolicyCreated>(Apply);
            Register<ApplicationMsgs.STSClientDetailsAdded>(Apply);
        }
        //Apply State Changes
        private void Apply(ApplicationMsgs.ApplicationCreated @event)
        {

            Id = @event.ApplicationId;
            _clientId = @event.Name;
            _oneRolePerUser = @event.OneRolePerUser;
        }

        private void Apply(ApplicationMsgs.PolicyCreated @event)
        {
            var policy = new SecurityPolicy(@event.PolicyId, @event.ClientId, this);
            if (DefaultPolicy == null) { DefaultPolicy = policy; }
            _policies.Add(@event.PolicyId, policy);
            _policyNames.Add(@event.ClientId);
        }

        private void Apply(ApplicationMsgs.STSClientDetailsAdded @event)
        {
            _stsClientDetailsConfigured = true;
        }
        //Public Methods

        /// <summary>
        /// Create a new Application.
        /// </summary>
        public SecuredApplication(
            Guid id,
            string defaultClientId,
            string version,
            bool oneRolePerUser,
            ICorrelatedMessage source)
            : base(source)
        {
            Ensure.NotEmptyGuid(id, nameof(id));
            Ensure.NotNullOrEmpty(defaultClientId, nameof(defaultClientId));
            Ensure.NotNullOrEmpty(version, nameof(version));
            Ensure.NotEmptyGuid(source.CorrelationId, nameof(source));
            RegisterEvents();
            Raise(new ApplicationMsgs.ApplicationCreated(
                         id,
                         defaultClientId,
                         version,
                         oneRolePerUser));
            Raise(new ApplicationMsgs.PolicyCreated(Guid.NewGuid(), defaultClientId, id, oneRolePerUser));
        }

        /// <summary>
        /// Retire an application that is no longer in use.
        /// </summary>
        public void Retire()
        {
            // Event should be idempotent in RMs, so no validation necessary.
            Raise(new ApplicationMsgs.ApplicationRetired(Id));
        }

        /// <summary>
        /// Re-activate a retired application that is being put back into use.
        /// </summary>
        public void Unretire()
        {
            // Event should be idempotent in RMs, so no validation necessary.
            Raise(new ApplicationMsgs.ApplicationUnretired(Id));
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
            Raise(new ApplicationMsgs.PolicyCreated(policyId, policyName, Id, _oneRolePerUser));
            return _policies[policyId];
        }
        public void AddSTSClientSecret(
            string encryptedClientSecret)
        {
            Ensure.NotNullOrEmpty(encryptedClientSecret, nameof(encryptedClientSecret));
            if (_stsClientDetailsConfigured)
            {
                Raise(new ApplicationMsgs.STSClientSecretAdded(Id, encryptedClientSecret));
            }
            else
            {
                Raise(new ApplicationMsgs.STSClientDetailsAdded(
                    Id,
                    _clientId,
                    new[] { "client_credentials", "password", "authorization_code" },
                    encryptedClientSecret,
                    new[] { "openid", "profile", "rd-policy", "enabled-policies" },
                    new[] { "http://localhost/elbe", "/root/signin-google" },
                    new[] { "http://localhost/elbe" },
                     "http://localhost/elbe" 
                    ));
            }
        }

        public void RemoveSTSClientSecret(
            string encryptedClientSecret)
        {
            Ensure.NotNullOrEmpty(encryptedClientSecret, nameof(encryptedClientSecret));
            Raise(new ApplicationMsgs.STSClientSecretRemoved(Id, encryptedClientSecret));
        }
    }
}
