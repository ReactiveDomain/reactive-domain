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
    public class SubjectAgg : AggregateRoot
    {
        //todo: set these from handling the creat event
        public string SubClaim { get; private set;}
        public string AuthProvider { get; private set;}
        public string AuthDomain { get; private set;}
        private SubjectAgg()
        {
            RegisterEvents();
        }

        private void RegisterEvents()
        {
           //todo:register events
        }
        
        /// <summary>
        /// Create a new subject for identity server.
        /// </summary>
        //todo: should we require a UserAgg here as the two are linked, might be a problem with unknown users.
        //Hmm? or a possiblity to include unknown users in a good workflow
        public SubjectAgg(
            Guid id,
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
            Ensure.NotEmptyGuid(source.CorrelationId, nameof(source.CorrelationId));
            if (source.CausationId == Guid.Empty)
                Ensure.NotEmptyGuid(source.MsgId, nameof(source.MsgId));

            ((ICorrelatedEventSource)this).Source = source;
            Raise(new SubjectMsgs.SubjectCreated(
                         id,
                         subClaim,
                         authProvider,
                         authDomain));
        }

        /// <summary>
        /// Log the fact that a user has been successfully authenticated.
        /// </summary>
        public void Authenticated(string hostIPAddress)
        {
            Raise(new SubjectMsgs.Authenticated(
                        Id,
                        DateTime.UtcNow,
                        hostIPAddress));
        }
        public void AddClientGrant(PolicyAgg policy){ 
            //todo: make this an idempotent no-op
            Raise(new SubjectMsgs.ClientGrantAdded(
                Id,
                policy.AppId,
                policy.Id
                ));
            }
        public void RemoveClientGrant(PolicyAgg policy){ 
            //todo: if grant not present just return
            Raise(new SubjectMsgs.ClientGrantRemoved(
                Id,
                policy.AppId,
                policy.Id
                ));
            }
        /// <summary>
        /// Log the fact that a user was granted access to a client.
        /// </summary>
        public void ClientAccessGranted(string clientAudience)
        {
            Raise(new SubjectMsgs.ClientAccessGranted(
                        Id,
                        DateTime.UtcNow,
                        clientAudience));
        }
        /// <summary>
        /// Log the fact that a user has atttemped to access a client without a client grant.
        /// </summary>
        public void ClientAccessDenied(string clientAudience)
        {
            Raise(new SubjectMsgs.ClientAccessDenied(
                        Id,
                        DateTime.UtcNow,
                        clientAudience));
        }
        
        /// <summary>
        /// Log the fact that a user has not been successfully authenticated.
        /// </summary>
        public void NotAuthenticated(string hostIPAddress)
        {
            Raise(new SubjectMsgs.AuthenticationFailed(
                        Id,
                        DateTime.UtcNow,
                        hostIPAddress));
        }
        /// <summary>
        /// Log the fact that a user has not been successfully authenticated because account is locked.
        /// </summary>
        public void NotAuthenticatedAccountLocked(string hostIPAddress)
        {
            Raise(new SubjectMsgs.AuthenticationFailedAccountLocked(
                        Id,
                        DateTime.UtcNow,
                        hostIPAddress));
        }
        /// <summary>
        /// Log the fact that a user has not been successfully authenticated because account is disabled.
        /// </summary>
        public void NotAuthenticatedAccountDisabled(string hostIPAddress)
        {
            Raise(new SubjectMsgs.AuthenticationFailedAccountDisabled(
                        Id,
                        DateTime.UtcNow,
                        hostIPAddress));
        }
        /// <summary>
        /// Log the fact that a user has not been successfully authenticated because invalid credentials were supplied.
        /// </summary>
        public void NotAuthenticatedInvalidCredentials(string hostIPAddress)
        {
            Raise(new SubjectMsgs.AuthenticationFailedInvalidCredentials(
                        Id,
                        DateTime.UtcNow,
                        hostIPAddress));
        }


      
       
    }
}
