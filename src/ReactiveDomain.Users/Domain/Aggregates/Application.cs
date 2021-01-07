using System;
using System.Collections.Generic;
using ReactiveDomain.Messaging;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Util;

namespace ReactiveDomain.Users.Domain.Aggregates
{
    /// <summary>
    /// Aggregate for a Application.
    /// </summary>
    public class Application : AggregateRoot
    {
        private Application()
        {
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            Register<ApplicationMsgs.ApplicationRegistered>(Apply);
        }

        private void Apply(ApplicationMsgs.ApplicationRegistered evt)
        {
            Id = evt.Id;
        }

        /// <summary>
        /// Create a new Application.
        /// </summary>
        public Application(
            Guid id,
            string name,
            bool oneRolePerUser,
            List<string> roles,
            string secAdminRole,
            string defaultUser,
            string defaultDomain,
            List<string> defaultUserRoles,
            ICorrelatedMessage source)
            : this()
        {
            Ensure.NotEmptyGuid(id, nameof(id));
            Ensure.NotNullOrEmpty(name, nameof(name));
            Ensure.NotNullOrEmpty(roles,nameof(roles));
            Ensure.NotNullOrEmpty(secAdminRole, nameof(secAdminRole));
            Ensure.NotNullOrEmpty(defaultUser, nameof(defaultUser));
            Ensure.NotNull(source, nameof(source));
            Ensure.NotEmptyGuid(source.CorrelationId, nameof(source.CorrelationId));
            if (source.CausationId == Guid.Empty)
                Ensure.NotEmptyGuid(source.MsgId, nameof(source.MsgId));

            ((ICorrelatedEventSource)this).Source = source;
            Raise(new ApplicationMsgs.ApplicationRegistered(
                         id,
                         name,
                         oneRolePerUser,
                         roles,
                         secAdminRole,
                         defaultUser,
                         defaultDomain,
                         defaultUserRoles));
        }

    }
}
