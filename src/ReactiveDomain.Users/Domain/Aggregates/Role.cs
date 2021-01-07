using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Util;

namespace ReactiveDomain.Users.Domain.Aggregates
{
    /// <summary>
    /// Aggregate for a Role.
    /// </summary>
    public class Role : AggregateRoot
    {
        private Role()
        {
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            Register<RoleMsgs.RoleCreated>(Apply);
            Register<RoleMsgs.RoleMigrated>(Apply);
        }

        private void Apply(RoleMsgs.RoleCreated evt)
        {
            Id = evt.RoleId;
        }
        private void Apply(RoleMsgs.RoleMigrated evt)
        {
            Id = evt.RoleId;
        }

        /// <summary>
        /// Create a new role.
        /// </summary>
        public Role(
                Guid id,
                string name,
                string application,
                ICorrelatedMessage source)
                : this()
        {
            Ensure.NotEmptyGuid(id, nameof(id));
            Ensure.NotNullOrEmpty(name, nameof(name));
            Ensure.NotNullOrEmpty(application, nameof(application));
            Ensure.NotNull(source, nameof(source));
            Ensure.NotEmptyGuid(source.CorrelationId, nameof(source.CorrelationId));
            if (source.CausationId == Guid.Empty)
                Ensure.NotEmptyGuid(source.MsgId, nameof(source.MsgId));

            ((ICorrelatedEventSource)this).Source = source;
            Raise(new RoleMsgs.RoleCreated(
                                id,
                                name,
                                application));
        }

        /// <summary>
        /// Remove a role.
        /// </summary>
        public void Remove()
        {
            Raise(new RoleMsgs.RoleRemoved(Id));
        }

    }
}