using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveDomain.Messaging;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Util;

namespace ReactiveDomain.Users.Domain.Aggregates
{
    /// <summary>
    /// Aggregate for a Application.
    /// </summary>
    public class SecuredApplicationAgg : AggregateRoot
    {
        private readonly Dictionary<Guid, SecurityPolicyAgg> _policies = new Dictionary<Guid, SecurityPolicyAgg>();
        private readonly HashSet<string> _policyNames = new HashSet<string>();
        

        // ReSharper disable once UnusedMember.Local
        // used via reflection in the repository
        private SecuredApplicationAgg()
        {
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            Register<ApplicationMsgs.ApplicationCreated>(Apply);
            Register<ApplicationMsgs.PolicyCreated>(Apply);
        }
        //Apply State Changes
        private void Apply(ApplicationMsgs.ApplicationCreated @event) => Id = @event.ApplicationId;

        private void Apply(ApplicationMsgs.PolicyCreated @event)
        {
            _policies.Add(@event.PolicyId, new SecurityPolicyAgg(@event.PolicyId, @event.PolicyName, this));
            _policyNames.Add(@event.PolicyName);
        }

        //Public Methods

        /// <summary>
        /// Create a new Application.
        /// </summary>
        public SecuredApplicationAgg(
            Guid id,
            string name,
            string version,
            ICorrelatedMessage source)
            : base(source)
        {
            Ensure.NotEmptyGuid(id, nameof(id));
            Ensure.NotNullOrEmpty(name, nameof(name));
            Ensure.NotNullOrEmpty(version, nameof(version));
            Ensure.NotEmptyGuid(source.CorrelationId, nameof(source));
            RegisterEvents();
            Raise(new ApplicationMsgs.ApplicationCreated(
                         id,
                         name,
                         version));
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
        public IReadOnlyList<SecurityPolicyAgg> Policies => _policies.Values.ToList().AsReadOnly();
        public SecurityPolicyAgg AddPolicy(Guid policyId, string policyName)
        {
            Ensure.NotEmptyGuid(policyId, nameof(policyId));
            Ensure.NotNullOrEmpty(policyName, nameof(policyName));
            if (_policies.ContainsKey(policyId) || _policyNames.Contains(policyName))
            {
                throw new InvalidOperationException($"Cannot add duplicate Policy: {{ Name:{policyName}, Id:{policyId} }}");
            }
            Raise(new ApplicationMsgs.PolicyCreated(policyId, policyName, Id));
            return _policies[policyId];
        }

    }
}
