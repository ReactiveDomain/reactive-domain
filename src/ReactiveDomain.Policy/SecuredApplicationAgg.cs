﻿using System;
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
        private readonly Dictionary<Guid, PolicyAgg> _policies = new Dictionary<Guid, PolicyAgg>();
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
            var policy = new PolicyAgg(@event.PolicyId, @event.ClientId, this);
            if (DefaultPolicy == null) { DefaultPolicy = policy; }
            _policies.Add(@event.PolicyId, policy);
            _policyNames.Add(@event.ClientId);
        }

        //Public Methods

        /// <summary>
        /// Create a new Application.
        /// </summary>
        public SecuredApplicationAgg(
            Guid id,
            string defaultClientId,
            string version,
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
                         version));
            Raise(new ApplicationMsgs.PolicyCreated(Guid.NewGuid(), defaultClientId, id));
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
        public PolicyAgg DefaultPolicy { get; private set;  }
        public IReadOnlyList<PolicyAgg> Policies => _policies.Values.ToList().AsReadOnly();
        public PolicyAgg AddAdditionalPolicy(Guid policyId, string policyName)
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