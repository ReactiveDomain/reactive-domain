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
    public class UserConfigurationSvc :
        TransientSubscriber,
        IHandleCommand<UserMsgs.CreateUser>,    
        IHandleCommand<UserMsgs.Deactivate>,
        IHandleCommand<UserMsgs.Activate>,
        IHandleCommand<UserMsgs.UpdateGivenName>,
        IHandleCommand<UserMsgs.UpdateSurname>,
        IHandleCommand<UserMsgs.UpdateFullName>,
        IHandleCommand<UserMsgs.UpdateEmail>,
        IHandleCommand<UserMsgs.UpdateAuthDomain>,
        IHandleCommand<UserMsgs.UpdateUserName>
    {

        private readonly CorrelatedStreamStoreRepository _repo;
        private readonly UsersRM _usersRm;
        private readonly ProvidersRM _providersRM;

        /// <summary>
        /// Create a service to act on User aggregates.
        /// </summary>
        /// <param name="repo">The repository for interacting with the EventStore.</param>
        /// <param name="bus">The dispatcher.</param>
        public UserConfigurationSvc(
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
            //Subscribe<IdentityMsgs.UserAuthenticated>(this);
            //Subscribe<IdentityMsgs.UserAuthenticationFailed>(this);
            //Subscribe<IdentityMsgs.UserAuthenticationFailedAccountLocked>(this);
            //Subscribe<IdentityMsgs.UserAuthenticationFailedAccountDisabled>(this);
            //Subscribe<IdentityMsgs.UserAuthenticationFailedInvalidCredentials>(this);
            //Subscribe<IdentityMsgs.UserAuthenticationFailedByExternalProvider>(this);
            Subscribe<UserMsgs.Activate>(this);
            Subscribe<UserMsgs.Deactivate>(this);
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

        public CommandResponse Handle(UserMsgs.Deactivate command)
        {
            var user = _repo.GetById<UserAgg>(command.UserId, command);
            user.Deactivate();
            _repo.Save(user);
            return command.Succeed();
        }

        public CommandResponse Handle(UserMsgs.Activate command)
        {
            var user = _repo.GetById<UserAgg>(command.UserId, command);
            user.Reactivate();
            _repo.Save(user);
            return command.Succeed();
        }
        /// <summary>
        /// Handle a UserMsgs.UpdateGivenName command.
        /// </summary>
        public CommandResponse Handle(UserMsgs.UpdateGivenName command)
        {
            var user = _repo.GetById<UserAgg>(command.UserId, command);
            user.UpdateGivenName(command.GivenName);
            _repo.Save(user);
            return command.Succeed();
        }
        /// <summary>
        /// Handle a UserMsgs.UpdateSurname command.
        /// </summary>
        public CommandResponse Handle(UserMsgs.UpdateSurname command)
        {
            var user = _repo.GetById<UserAgg>(command.UserId, command);
            user.UpdateSurname(command.Surname);
            _repo.Save(user);
            return command.Succeed();
        }

        /// <summary>
        /// Handle a UserMsgs.UpdateFullName command.
        /// </summary>
        public CommandResponse Handle(UserMsgs.UpdateFullName command)
        {
            var user = _repo.GetById<UserAgg>(command.UserId, command);
            user.UpdateFullName(command.FullName);
            _repo.Save(user);
            return command.Succeed();
        }

        /// <summary>
        /// Handle a UserMsgs.UpdateEmail command.
        /// </summary>
        public CommandResponse Handle(UserMsgs.UpdateEmail command)
        {
            var user = _repo.GetById<UserAgg>(command.UserId, command);
            user.UpdateEmail(command.Email);
            _repo.Save(user);
            return command.Succeed();
        }
        
        /// <summary>
        /// Handle a UserMsgs.UpdateAuthDomain command.
        /// </summary>
        public CommandResponse Handle(UserMsgs.UpdateAuthDomain command)
        {
            var user = _repo.GetById<UserAgg>(command.UserId, command);
            user.UpdateAuthDomain(command.AuthDomain);
            _repo.Save(user);
            return command.Succeed();
        }
        /// <summary>
        /// Handle a UserMsgs.UpdateUserName command.
        /// </summary>
        public CommandResponse Handle(UserMsgs.UpdateUserName command)
        {
            var user = _repo.GetById<UserAgg>(command.UserId, command);
            user.UpdateUserName(command.UserName);
            _repo.Save(user);
            return command.Succeed();
        }
    }
}
