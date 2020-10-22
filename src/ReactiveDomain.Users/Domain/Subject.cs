using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Util;

namespace ReactiveDomain.Users.Domain
{
    /// <summary>
    /// User authentication and login.
    /// </summary>
    public class Subject : AggregateRoot
    {
        private Subject()
        {
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            Register<SubjectMsgs.SubjectCreated>(@event => Id = @event.SubjectId);
        }

        /// <summary>
        /// Create a new subject for identity server.
        /// </summary>
        public Subject(
            Guid id,
            Guid userId,
            string subClaim,
            string authProvider,
            string authDomain,
            ICorrelatedMessage source)
            : this()
        {
            Ensure.NotEmptyGuid(id, nameof(id));
            Ensure.NotNullOrEmpty(authProvider, nameof(authProvider));
            Ensure.NotNullOrEmpty(authDomain, nameof(authDomain));
            Ensure.NotNullOrEmpty(subClaim, nameof(subClaim));
            Ensure.NotNull(source, nameof(source));
            Ensure.NotEmptyGuid(userId, nameof(userId));
            Ensure.NotEmptyGuid(source.CorrelationId, nameof(source.CorrelationId));
            if (source.CausationId == Guid.Empty)
                Ensure.NotEmptyGuid(source.MsgId, nameof(source.MsgId));

            ((ICorrelatedEventSource)this).Source = source;
            Raise(new SubjectMsgs.SubjectCreated(
                         id,
                         userId,
                         subClaim,
                         authProvider,
                         authDomain));
        }

        /// <summary>
        /// Log the fact that a user has been successfully authenticated.
        /// </summary>
        public void Authenticated(string hostIpAddress, string clientId)
        {
            Raise(new SubjectMsgs.Authenticated(
                        Id,
                        DateTime.UtcNow,
                        hostIpAddress,
                        clientId));
        }

        /// <summary>
        /// Log the fact that a user has not been successfully authenticated because account is locked.
        /// </summary>
        public void NotAuthenticatedAccountLocked(string hostIpAddress, string clientId)
        {
            Raise(new SubjectMsgs.AuthenticationFailedAccountLocked(
                        Id,
                        DateTime.UtcNow,
                        hostIpAddress,
                        clientId));
        }
        /// <summary>
        /// Log the fact that a user has not been successfully authenticated because account is disabled.
        /// </summary>
        public void NotAuthenticatedAccountDisabled(string hostIpAddress, string clientId)
        {
            Raise(new SubjectMsgs.AuthenticationFailedAccountDisabled(
                        Id,
                        DateTime.UtcNow,
                        hostIpAddress,
                        clientId));
        }
        /// <summary>
        /// Log the fact that a user has not been successfully authenticated because invalid credentials were supplied.
        /// </summary>
        public void NotAuthenticatedInvalidCredentials(string hostIpAddress, string clientId)
        {
            Raise(new SubjectMsgs.AuthenticationFailedInvalidCredentials(
                        Id,
                        DateTime.UtcNow,
                        hostIpAddress,
                        clientId));
        }
    }
}
