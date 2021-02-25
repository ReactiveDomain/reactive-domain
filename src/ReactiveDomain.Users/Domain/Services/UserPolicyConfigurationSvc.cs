using System;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Users.Domain.Aggregates;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Users.ReadModels;

namespace ReactiveDomain.Users.Domain.Services
{
    /// <summary>
    /// The service that fronts the User aggregate.
    /// </summary>
    public class UserPolicyConfigurationSvc :
        TransientSubscriber,      
        IHandleCommand<UserMsgs.AddPolicy>,
        IHandleCommand<UserMsgs.AddApplication>,
        IHandleCommand<UserMsgs.AssignRole>,
        IHandleCommand<UserMsgs.UnassignRole>
       
    {

        private readonly CorrelatedStreamStoreRepository _repo;
        private readonly UsersRM _usersRm;
        private readonly ProvidersRM _providersRM;

        /// <summary>
        /// Create a service to act on User aggregates.
        /// </summary>
        /// <param name="repo">The repository for interacting with the EventStore.</param>
        /// <param name="bus">The dispatcher.</param>
        public UserPolicyConfigurationSvc(
            string schema,
            Func<IListener> getListener,
            IRepository repo,
            IDispatcher bus)
            : base(bus)
        {
            _repo = new CorrelatedStreamStoreRepository(repo);
            _usersRm = new UsersRM(getListener);
            _providersRM = new ProvidersRM(schema, getListener);

            Subscribe<UserMsgs.CreateUser>(this);
            Subscribe<IdentityMsgs.UserAuthenticated>(this);
            Subscribe<IdentityMsgs.UserAuthenticationFailed>(this);
            Subscribe<IdentityMsgs.UserAuthenticationFailedAccountLocked>(this);
            Subscribe<IdentityMsgs.UserAuthenticationFailedAccountDisabled>(this);
            Subscribe<IdentityMsgs.UserAuthenticationFailedInvalidCredentials>(this);
            Subscribe<IdentityMsgs.UserAuthenticationFailedByExternalProvider>(this);
            Subscribe<UserMsgs.Activate>(this);
            Subscribe<UserMsgs.Deactivate>(this);
            Subscribe<UserMsgs.AssignRole>(this);
            Subscribe<UserMsgs.UnassignRole>(this);
            Subscribe<UserMsgs.UpdateGivenName>(this);
            Subscribe<UserMsgs.UpdateSurname>(this);
            Subscribe<UserMsgs.UpdateFullName>(this);
            Subscribe<UserMsgs.UpdateEmail>(this);
            Subscribe<UserMsgs.UpdateAuthDomain>(this);
            Subscribe<UserMsgs.UpdateUserName>(this);
        }

        private bool _disposed;

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (_disposed) return;
            if (disposing)
            {
                _usersRm.Dispose();
                _providersRM.Dispose();
            }
            _disposed = true;
        }
   
    }
}
