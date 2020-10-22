using System;
using ReactiveDomain.Foundation;
using ReactiveDomain.Identity.Storage.Domain.Aggregates;
using ReactiveDomain.Identity.Storage.Messages;
using ReactiveDomain.Logging;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Identity.Storage.Domain.Services
{
    /// <summary>
    /// The service that fronts the User aggregate.
    /// </summary>
    public class UserSvc :
        TransientSubscriber,
        IHandleCommand<UserMsgs.CreateUser>,
        IHandle<IdentityMsgs.UserAuthenticated>,
        IHandle<IdentityMsgs.UserAuthenticationFailed>,
        IHandle<IdentityMsgs.UserAuthenticationFailedAccountLocked>,
        IHandle<IdentityMsgs.UserAuthenticationFailedAccountDisabled>,
        IHandle<IdentityMsgs.UserAuthenticationFailedInvalidCredentials>,
        IHandle<IdentityMsgs.UserAuthenticationFailedByExternalProvider>,
        IHandleCommand<UserMsgs.AssignRole>,
        IHandleCommand<UserMsgs.UnassignRole>,
        IHandleCommand<UserMsgs.Deactivate>,
        IHandleCommand<UserMsgs.Activate>,
        IHandleCommand<UserMsgs.UpdateGivenName>,
        IHandleCommand<UserMsgs.UpdateSurname>,
        IHandleCommand<UserMsgs.UpdateFullName>,
        IHandleCommand<UserMsgs.UpdateEmail>,
        IHandleCommand<UserMsgs.UpdateUserSidFromAuthProvider>,
        IHandleCommand<UserMsgs.UpdateAuthDomain>,
        IHandleCommand<UserMsgs.UpdateUserName>
    {
        private static readonly ILogger Log = LogManager.GetLogger(Bootstrap.LogName);

        private readonly CorrelatedStreamStoreRepository _repo;
        private readonly UsersRM _usersRm;
        private readonly ProvidersRM _providersRM;

        /// <summary>
        /// Create a service to act on User aggregates.
        /// </summary>
        /// <param name="repo">The repository for interacting with the EventStore.</param>
        /// <param name="bus">The dispatcher.</param>
        public UserSvc(
            IRepository repo,
            IDispatcher bus,
            Func<string, IListener> getListener)
            : base(bus)
        {
            _repo = new CorrelatedStreamStoreRepository(repo);
            _usersRm = new UsersRM(getListener);
            _providersRM = new ProvidersRM(getListener);

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
            Subscribe<UserMsgs.UpdateUserSidFromAuthProvider>(this);
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
                            command.UserName,
                            command.UserSidFromAuthProvider))
            {
                throw new DuplicateUserException(
                            command.AuthProvider,
                            command.AuthDomain,
                            command.UserName);
            }
            var user = new User(
                            command.Id,
                            command.UserSidFromAuthProvider,
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
            User user;
            if (_usersRm.TryGetUserId(message.AuthProvider, message.AuthDomain, message.UserName, message.UserSidFromAuthProvider, out var id))
            {
                user = _repo.GetById<User>(id, message);
            }
            else
            {
                user = new User(
                            Guid.NewGuid(),
                            message.UserSidFromAuthProvider,
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
        }
        /// <summary>
        /// Handle a IdentityMsgs.UserAuthenticationFailed event from an external source.
        /// </summary>
        public void Handle(IdentityMsgs.UserAuthenticationFailed message)
        {
            User user;
            if (_usersRm.TryGetUserId(message.AuthProvider, message.AuthDomain, message.UserName, string.Empty, out var id))
            {
                user = _repo.GetById<User>(id, message);
            }
            else
            {
                user = new User(
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
        }
        /// <summary>
        /// Handle a IdentityMsgs.UserAuthenticationFailedAccountLocked event from an external source.
        /// </summary>
        public void Handle(IdentityMsgs.UserAuthenticationFailedAccountLocked message)
        {
            User user;
            if (_usersRm.TryGetUserId(message.AuthProvider, message.AuthDomain, message.UserName, string.Empty, out var id))
            {
                user = _repo.GetById<User>(id, message);
            }
            else
            {
                user = new User(
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
        }
        /// <summary>
        /// Handle a IdentityMsgs.UserAuthenticationFailedAccountDisabled event from an external source.
        /// </summary>
        public void Handle(IdentityMsgs.UserAuthenticationFailedAccountDisabled message)
        {
            User user;
            if (_usersRm.TryGetUserId(message.AuthProvider, message.AuthDomain, message.UserName, string.Empty, out var id))
            {
                user = _repo.GetById<User>(id, message);
            }
            else
            {
                user = new User(
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
        }
        /// <summary>
        /// Handle a IdentityMsgs.UserAuthenticationFailedInvalidCredentials event from an external source.
        /// </summary>
        public void Handle(IdentityMsgs.UserAuthenticationFailedInvalidCredentials message)
        {
            User user;
            if (_usersRm.TryGetUserId(message.AuthProvider, message.AuthDomain, message.UserName, string.Empty, out var id))
            {
                user = _repo.GetById<User>(id, message);
            }
            else
            {
                user = new User(
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

        public CommandResponse Handle(UserMsgs.AssignRole command)
        {
            var user = _repo.GetById<User>(command.UserId, command);
            user.AssignRole(command.RoleId);
            _repo.Save(user);
            return command.Succeed();
        }

        public CommandResponse Handle(UserMsgs.UnassignRole command)
        {
            var user = _repo.GetById<User>(command.UserId, command);
            user.UnassignRole(command.RoleId);
            _repo.Save(user);
            return command.Succeed();
        }

        public CommandResponse Handle(UserMsgs.Deactivate command)
        {
            var user = _repo.GetById<User>(command.UserId, command);
            user.Deactivate();
            _repo.Save(user);
            return command.Succeed();
        }

        public CommandResponse Handle(UserMsgs.Activate command)
        {
            var user = _repo.GetById<User>(command.UserId, command);
            user.Reactivate();
            _repo.Save(user);
            return command.Succeed();
        }
        /// <summary>
        /// Handle a UserMsgs.UpdateGivenName command.
        /// </summary>
        public CommandResponse Handle(UserMsgs.UpdateGivenName command)
        {
            var user = _repo.GetById<User>(command.UserId, command);
            user.UpdateGivenName(command.GivenName);
            _repo.Save(user);
            return command.Succeed();
        }
        /// <summary>
        /// Handle a UserMsgs.UpdateSurname command.
        /// </summary>
        public CommandResponse Handle(UserMsgs.UpdateSurname command)
        {
            var user = _repo.GetById<User>(command.UserId, command);
            user.UpdateSurname(command.Surname);
            _repo.Save(user);
            return command.Succeed();
        }

        /// <summary>
        /// Handle a UserMsgs.UpdateFullName command.
        /// </summary>
        public CommandResponse Handle(UserMsgs.UpdateFullName command)
        {
            var user = _repo.GetById<User>(command.UserId, command);
            user.UpdateFullName(command.FullName);
            _repo.Save(user);
            return command.Succeed();
        }

        /// <summary>
        /// Handle a UserMsgs.UpdateEmail command.
        /// </summary>
        public CommandResponse Handle(UserMsgs.UpdateEmail command)
        {
            var user = _repo.GetById<User>(command.UserId, command);
            user.UpdateEmail(command.Email);
            _repo.Save(user);
            return command.Succeed();
        }
        /// <summary>
        /// Handle a UserMsgs.UpdateUserSidFromAuthProvider command.
        /// </summary>
        public CommandResponse Handle(UserMsgs.UpdateUserSidFromAuthProvider command)
        {
            var user = _repo.GetById<User>(command.UserId, command);
            user.UpdateUserSidFromAuthProvider(command.UserSidFromAuthProvider);
            _repo.Save(user);
            return command.Succeed();
        }
        /// <summary>
        /// Handle a UserMsgs.UpdateAuthDomain command.
        /// </summary>
        public CommandResponse Handle(UserMsgs.UpdateAuthDomain command)
        {
            var user = _repo.GetById<User>(command.UserId, command);
            user.UpdateAuthDomain(command.AuthDomain);
            _repo.Save(user);
            return command.Succeed();
        }
        /// <summary>
        /// Handle a UserMsgs.UpdateUserName command.
        /// </summary>
        public CommandResponse Handle(UserMsgs.UpdateUserName command)
        {
            var user = _repo.GetById<User>(command.UserId, command);
            user.UpdateUserName(command.UserName);
            _repo.Save(user);
            return command.Succeed();
        }
    }
}
