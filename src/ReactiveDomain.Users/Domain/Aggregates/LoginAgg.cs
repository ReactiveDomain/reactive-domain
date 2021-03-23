using System;
using System.Collections.Generic;
using System.Net.Mail;
using ReactiveDomain.Messaging;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Util;

namespace ReactiveDomain.Users.Domain.Aggregates
{
    /// <summary>
    /// User authentication and login.
    /// </summary>
    public class LoginAgg : AggregateRoot
    {
        private LoginAgg()
        {
            RegisterEvents();
        }

        private void RegisterEvents()
        {
           //todo:register events
        }
        
        /// <summary>
        /// Create a new user login tracker.
        /// </summary>
        //todo: should we require a UserAgg here as the two are linked, might be a problem with unknown users.
        //Hmm? or a possiblity to include unknown users in a good workflow
        public LoginAgg(
            Guid id,
            Guid userId,
            string subjectId,
            string authProvider,
            string authDomain,          
            ICorrelatedMessage source)
            : this()
        {
            Ensure.NotEmptyGuid(id, nameof(id));
            Ensure.NotEmptyGuid(userId, nameof(id));
            Ensure.NotNullOrEmpty(authProvider, nameof(authProvider));
            Ensure.NotNullOrEmpty(authDomain, nameof(authDomain));
            Ensure.NotNullOrEmpty(subjectId, nameof(subjectId));           
            Ensure.NotNull(source, nameof(source));
            Ensure.NotEmptyGuid(source.CorrelationId, nameof(source.CorrelationId));
            if (source.CausationId == Guid.Empty)
                Ensure.NotEmptyGuid(source.MsgId, nameof(source.MsgId));

            ((ICorrelatedEventSource)this).Source = source;
            Raise(new UserAuthMsgs.UserAuthHistoryCreated(
                         id,
                         subjectId,
                         authProvider,
                         authDomain));
        }

        /// <summary>
        /// Log the fact that a user has been successfully authenticated.
        /// </summary>
        public void Authenticated(string hostIPAddress)
        {
            Raise(new UserAuthMsgs.Authenticated(
                        Id,
                        DateTime.UtcNow,
                        hostIPAddress));
        }
        /// <summary>
        /// Log the fact that a user has not been successfully authenticated.
        /// </summary>
        public void NotAuthenticated(string hostIPAddress)
        {
            Raise(new UserAuthMsgs.AuthenticationFailed(
                        Id,
                        DateTime.UtcNow,
                        hostIPAddress));
        }
        /// <summary>
        /// Log the fact that a user has not been successfully authenticated because account is locked.
        /// </summary>
        public void NotAuthenticatedAccountLocked(string hostIPAddress)
        {
            Raise(new UserAuthMsgs.AuthenticationFailedAccountLocked(
                        Id,
                        DateTime.UtcNow,
                        hostIPAddress));
        }
        /// <summary>
        /// Log the fact that a user has not been successfully authenticated because account is disabled.
        /// </summary>
        public void NotAuthenticatedAccountDisabled(string hostIPAddress)
        {
            Raise(new UserAuthMsgs.AuthenticationFailedAccountDisabled(
                        Id,
                        DateTime.UtcNow,
                        hostIPAddress));
        }
        /// <summary>
        /// Log the fact that a user has not been successfully authenticated because invalid credentials were supplied.
        /// </summary>
        public void NotAuthenticatedInvalidCredentials(string hostIPAddress)
        {
            Raise(new UserAuthMsgs.AuthenticationFailedInvalidCredentials(
                        Id,
                        DateTime.UtcNow,
                        hostIPAddress));
        }


      
       
    }
}
