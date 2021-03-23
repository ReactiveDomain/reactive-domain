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
    public class UserLoginSvc :
        TransientSubscriber,        
        IHandle<IdentityMsgs.UserAuthenticated>,
        IHandle<IdentityMsgs.UserAuthenticationFailed>,
        IHandle<IdentityMsgs.UserAuthenticationFailedAccountLocked>,
        IHandle<IdentityMsgs.UserAuthenticationFailedAccountDisabled>,
        IHandle<IdentityMsgs.UserAuthenticationFailedInvalidCredentials>,
        IHandle<IdentityMsgs.UserAuthenticationFailedByExternalProvider>
      
    {

        private readonly CorrelatedStreamStoreRepository _repo;
        private readonly UsersRM _usersRm;
        private readonly ProvidersRM _providersRM;

        /// <summary>
        /// Create a service to act on User aggregates.
        /// </summary>
        /// <param name="repo">The repository for interacting with the EventStore.</param>
        /// <param name="bus">The dispatcher.</param>
        public UserLoginSvc(
            string schema,
            Func<IListener> getListener,
            IRepository repo,
            IDispatcher bus)
            : base(bus)
        {
            _repo = new CorrelatedStreamStoreRepository(repo);
            _usersRm = new UsersRM(getListener);
            _providersRM = new ProvidersRM(schema, getListener);
           
            Subscribe<IdentityMsgs.UserAuthenticated>(this);
            Subscribe<IdentityMsgs.UserAuthenticationFailed>(this);
            Subscribe<IdentityMsgs.UserAuthenticationFailedAccountLocked>(this);
            Subscribe<IdentityMsgs.UserAuthenticationFailedAccountDisabled>(this);
            Subscribe<IdentityMsgs.UserAuthenticationFailedInvalidCredentials>(this);
            Subscribe<IdentityMsgs.UserAuthenticationFailedByExternalProvider>(this);
           
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

        /// <summary>
        /// Handle a UserMsgs.CreateUser command.
        /// </summary>
        public CommandResponse Handle(UserMsgs.CreateUser command)
        {
            if (_usersRm.UserExists(
                            command.AuthProvider,
                            command.AuthDomain,
                            command.SubjectId,
                            out _))
            {
                throw new DuplicateUserException(
                            command.AuthProvider,
                            command.AuthDomain,
                            command.UserName);
            }
            var user = new UserAgg(
                            command.Id,
                            command.SubjectId,
                            command.AuthProvider,
                            command.AuthDomain,
                            command.UserName,
                            command.FullName,
                            command.GivenName,
                            command.Surname,
                            command.Email,
                            command);
            _repo.Save(user);
            return command.Succeed();
        }

        /// <summary>
        /// Handle a IdentityMsgs.UserAuthenticated event from an external source.
        /// </summary>
        public void Handle(IdentityMsgs.UserAuthenticated message)
        {
            throw new NotImplementedException();
            /*
            UserAgg user;
            if (_usersRm.TryGetUserId(message.AuthProvider, message.AuthDomain,  message.SubjectId, out var id))
            {
                user = _repo.GetById<UserAgg>(id, message);
            }
            else
            {
                user = new UserAgg(
                            Guid.NewGuid(),
                            message.SubjectId,
                            message.AuthProvider,
                            message.AuthDomain,
                            message.UserName,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            message);
            }
            user.Authenticated(message.HostIPAddress);
            _repo.Save(user);
            */
        }
        /// <summary>
        /// Handle a IdentityMsgs.UserAuthenticationFailed event from an external source.
        /// </summary>
        public void Handle(IdentityMsgs.UserAuthenticationFailed message)
        {
            //todo:clc- address challenge with finding user without sub Claim
            // do we need to do this?
               throw new NotImplementedException();
            /*
            UserAgg user;
            if (_usersRm.TryGetUserId(message.AuthProvider, message.AuthDomain, message.UserName, out var id))
            {
                user = _repo.GetById<UserAgg>(id, message);
            }
            else
            {
                user = new UserAgg(
                            Guid.NewGuid(),
                            string.Empty,
                            message.AuthProvider,
                            message.AuthDomain,
                            message.UserName,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            message);
            }
            user.NotAuthenticated(message.HostIPAddress);
            _repo.Save(user);
            */
        }
        /// <summary>
        /// Handle a IdentityMsgs.UserAuthenticationFailedAccountLocked event from an external source.
        /// </summary>
        public void Handle(IdentityMsgs.UserAuthenticationFailedAccountLocked message)
        {
            //todo:clc- address challenge with finding user without sub Claim
            // do we need to do this?
               throw new NotImplementedException();
            /*
            UserAgg user;
            if (_usersRm.TryGetUserId(message.AuthProvider, message.AuthDomain, message.UserName,  out var id))
            {
                user = _repo.GetById<UserAgg>(id, message);
            }
            else
            {
                user = new UserAgg(
                            Guid.NewGuid(),
                            string.Empty,
                            message.AuthProvider,
                            message.AuthDomain,
                            message.UserName,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            message);
            }
            user.NotAuthenticatedAccountLocked(message.HostIPAddress);
            _repo.Save(user);
            */
        }
        /// <summary>
        /// Handle a IdentityMsgs.UserAuthenticationFailedAccountDisabled event from an external source.
        /// </summary>
        public void Handle(IdentityMsgs.UserAuthenticationFailedAccountDisabled message)
        {
            //todo:clc- address challenge with finding user without sub Claim
            // do we need to do this?
               throw new NotImplementedException();
            /*
            UserAgg user;
            if (_usersRm.TryGetUserId(message.AuthProvider, message.AuthDomain, message.UserName, out var id))
            {
                user = _repo.GetById<UserAgg>(id, message);
            }
            else
            {
                user = new UserAgg(
                            Guid.NewGuid(),
                            string.Empty,
                            message.AuthProvider,
                            message.AuthDomain,
                            message.UserName,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            message);
            }
            user.NotAuthenticatedAccountDisabled(message.HostIPAddress);
            _repo.Save(user);
            */
        }
        /// <summary>
        /// Handle a IdentityMsgs.UserAuthenticationFailedInvalidCredentials event from an external source.
        /// </summary>
        public void Handle(IdentityMsgs.UserAuthenticationFailedInvalidCredentials message)
        {
            //todo:clc- address challenge with finding user without sub Claim
            // do we need to do this?
               throw new NotImplementedException();
            /*
            UserAgg user;
            if (_usersRm.TryGetUserId(message.AuthProvider, message.AuthDomain, message.UserName, out var id))
            {
                user = _repo.GetById<UserAgg>(id, message);
            }
            else
            {
                user = new UserAgg(
                            Guid.NewGuid(),
                            string.Empty,
                            message.AuthProvider,
                            message.AuthDomain,
                            message.UserName,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            message);
            }
            user.NotAuthenticatedInvalidCredentials(message.HostIPAddress);
            _repo.Save(user);
            */
        }

        /// <summary>
        /// Handle a IdentityMsgs.UserAuthenticationFailedByExternalProvider event from an external source.
        /// </summary>
        public void Handle(IdentityMsgs.UserAuthenticationFailedByExternalProvider message)
        {
            ExternalProvider provider;
            if (_providersRM.TryGetProviderId(message.AuthProvider, out var id))
            {
                provider = _repo.GetById<ExternalProvider>(id, message);
            }
            else
            {
                provider = new ExternalProvider(
                                Guid.NewGuid(),
                                message.AuthProvider,
                                message);
            }
            provider.NotAuthenticatedInvalidCredentials(message.HostIPAddress);
            _repo.Save(provider);
        }     
       
    }
}
