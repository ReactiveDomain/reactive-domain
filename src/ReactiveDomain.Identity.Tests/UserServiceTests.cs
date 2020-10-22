using System;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Testing;
using ReactiveDomain.Users.Domain;
using ReactiveDomain.Users.Domain.Aggregates;
using ReactiveDomain.Users.Domain.Services;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Users.Tests.Helpers;
using Xunit;

namespace ReactiveDomain.Users.Tests
{
    [Collection("UserDomainTests")]
    public sealed class UserServiceTests :
        IDisposable
    {
        private readonly ICorrelatedMessage _command = MessageBuilder.New(() => new TestMessages.RootCommand());

        private readonly MockRepositorySpecification _fixture;
        private readonly UserSvc _userSvc;
        private readonly RoleSvc _roleSvc;

        private readonly Guid _userId = Guid.NewGuid();
        private readonly string _userSidFromAuthProvider = Guid.NewGuid().ToString();
        private const string AuthProvider = Constants.AuthenticationProviderAD;
        private const string AuthDomain = "Perkinelmernet";
        private const string UserName = "jsmith";
        private const string GivenName = "John";
        private const string Surname = "Smith";
        private const string FullName = "John Smith";
        private const string Email = "john.smith@perkinelmer.com";
        private const string AuthenticationError = "invalid username or password";
        private const string GiveNameUpdate = "John Update";
        private const string SurnameUpdate = "Smith Update";
        private const string FullNameUpdate = "John Smith Update";
        private const string EmailUpdate = "john.smithUpdate@perkinelmer.com";
        private readonly string _userSidFromAuthProviderUpdate = Guid.NewGuid() + "_Update";
        private const string AuthDomainUpdate = "Perkinelmernet Update";
        private const string UserNameUpdate = "jsmithupdate";

        private readonly Guid _roleId = Guid.NewGuid();
        private const string RoleName = "Admin";
        private const string Application = "Kaleido";
        private const string HostIPAddress = "127.0.0.1";

        public UserServiceTests()
        {
            _fixture = new MockRepositorySpecification();
            _userSvc = new UserSvc(_fixture.Schema, () => _fixture.GetListener(nameof(RoleServiceTests)), _fixture.Repository, _fixture.Dispatcher);
            _roleSvc = new RoleSvc(() => _fixture.GetListener(nameof(RoleServiceTests)), _fixture.Repository, _fixture.Dispatcher);
        }

        public void Dispose()
        {
            _userSvc.Dispose();
            _roleSvc.Dispose();
        }

        [Fact]
        public void can_create_new_user()
        {
            var cmd = MessageBuilder.New(
                        () => new UserMsgs.CreateUser(
                                    _userId,
                                    _userSidFromAuthProvider,
                                    AuthProvider,
                                    AuthDomain,
                                    UserName,
                                    FullName,
                                    GivenName,
                                    Surname,
                                    Email));
            _fixture.Dispatcher.Send(cmd);

            _fixture.RepositoryEvents.WaitFor<UserMsgs.UserCreated>(TimeSpan.FromMilliseconds(100));
            _fixture
                .TestQueue
                .AssertNext<UserMsgs.CreateUser>(cmd.CorrelationId)
                .AssertEmpty();
            _fixture
                .RepositoryEvents
                .AssertNext<UserMsgs.UserCreated>(cmd.CorrelationId)
                .AssertEmpty();
        }

        [Fact]
        public void cannot_create_duplicate_user()
        {
            AddUser();
            var cmd = MessageBuilder.New(
                        () => new UserMsgs.CreateUser(
                                    _userId,
                                    _userSidFromAuthProvider,
                                    AuthProvider,
                                    AuthDomain,
                                    UserName,
                                    FullName,
                                    GivenName,
                                    Surname,
                                    Email));
            AssertEx.CommandThrows<DuplicateUserException>(() => _fixture.Dispatcher.Send(cmd));
            _fixture
                .TestQueue
                .AssertNext<UserMsgs.CreateUser>(cmd.CorrelationId)
                .AssertEmpty();
            _fixture.RepositoryEvents.AssertEmpty();
        }

        [Fact]
        public void can_log_authentication()
        {
            AddUser();

            var msg = MessageBuilder
                        .From(_command)
                        .Build(() => new IdentityMsgs.UserAuthenticated(
                                            AuthProvider,
                                            AuthDomain,
                                            UserName,
                                            _userSidFromAuthProvider,
                                            HostIPAddress));

            _fixture.Dispatcher.Publish(msg);

            _fixture.RepositoryEvents.WaitFor<UserMsgs.Authenticated>(TimeSpan.FromMilliseconds(100));
            _fixture
                .TestQueue
                .AssertNext<IdentityMsgs.UserAuthenticated>(_command.CorrelationId)
                .AssertEmpty();
            _fixture
                .RepositoryEvents
                .AssertNext<UserMsgs.Authenticated>(_command.CorrelationId)
                .AssertEmpty();
        }

        [Fact]
        public void can_log_unknown_user_authenticated()
        {
            var msg = MessageBuilder
                        .From(_command)
                        .Build(() => new IdentityMsgs.UserAuthenticated(
                                            AuthProvider,
                                            AuthDomain,
                                            UserName,
                                            _userSidFromAuthProvider,
                                            HostIPAddress));

            _fixture.Dispatcher.Publish(msg);

            _fixture.RepositoryEvents.WaitFor<UserMsgs.Authenticated>(TimeSpan.FromMilliseconds(100));

            _fixture
                .TestQueue
                .AssertNext<IdentityMsgs.UserAuthenticated>(_command.CorrelationId)
                .AssertEmpty();

            _fixture
                .RepositoryEvents
                .AssertNext<UserMsgs.UserCreated>(_command.CorrelationId, out var created)
                .AssertNext<UserMsgs.Authenticated>(_command.CorrelationId, out var authenticated)
                .AssertEmpty();

            Assert.Equal(AuthProvider, created.AuthProvider);
            Assert.Equal(AuthDomain, created.AuthDomain);
            Assert.Equal(UserName, created.UserName);
            Assert.Equal(_userSidFromAuthProvider, created.SubjectId);
            Assert.Equal(created.Id, authenticated.Id);
            Assert.Equal(HostIPAddress, authenticated.HostIPAddress);
        }

        [Fact]
        public void can_log_unknown_user_authentication_failed()
        {
            var msg = MessageBuilder
                        .From(_command)
                        .Build(() => new IdentityMsgs.UserAuthenticationFailed(
                                            AuthProvider,
                                            AuthDomain,
                                            UserName,
                                            AuthenticationError,
                                            HostIPAddress));
            _fixture.Dispatcher.Publish(msg);

            _fixture.RepositoryEvents.WaitFor<UserMsgs.AuthenticationFailed>(TimeSpan.FromMilliseconds(100));

            _fixture
                .TestQueue
                .AssertNext<IdentityMsgs.UserAuthenticationFailed>(_command.CorrelationId)
                .AssertEmpty();

            _fixture
                .RepositoryEvents
                .AssertNext<UserMsgs.UserCreated>(_command.CorrelationId, out var created)
                .AssertNext<UserMsgs.AuthenticationFailed>(_command.CorrelationId, out var failed)
                .AssertEmpty();

            Assert.Equal(AuthProvider, created.AuthProvider);
            Assert.Equal(AuthDomain, created.AuthDomain);
            Assert.Equal(UserName, created.UserName);
            Assert.Equal(created.Id, failed.Id);
            Assert.Equal(HostIPAddress, failed.HostIPAddress);
        }

        [Fact]
        public void can_log_unknown_user_account_locked()
        {
            var msg = MessageBuilder
                        .From(_command)
                        .Build(() => new IdentityMsgs.UserAuthenticationFailedAccountLocked(
                                            AuthProvider,
                                            AuthDomain,
                                            UserName,
                                            HostIPAddress));
            _fixture.Dispatcher.Publish(msg);

            _fixture.RepositoryEvents.WaitFor<UserMsgs.AuthenticationFailedAccountLocked>(TimeSpan.FromMilliseconds(100));

            _fixture
                .TestQueue
                .AssertNext<IdentityMsgs.UserAuthenticationFailedAccountLocked>(_command.CorrelationId)
                .AssertEmpty();

            _fixture
                .RepositoryEvents
                .AssertNext<UserMsgs.UserCreated>(_command.CorrelationId, out var created)
                .AssertNext<UserMsgs.AuthenticationFailedAccountLocked>(_command.CorrelationId, out var failed)
                .AssertEmpty();

            Assert.Equal(AuthProvider, created.AuthProvider);
            Assert.Equal(AuthDomain, created.AuthDomain);
            Assert.Equal(UserName, created.UserName);
            Assert.Equal(created.Id, failed.Id);
            Assert.Equal(HostIPAddress, failed.HostIPAddress);
        }

        [Fact]
        public void can_log_unknown_user_account_disabled()
        {
            var msg = MessageBuilder
                .From(_command)
                .Build(() => new IdentityMsgs.UserAuthenticationFailedAccountDisabled(
                    AuthProvider,
                    AuthDomain,
                    UserName,
                    HostIPAddress));
            _fixture.Dispatcher.Publish(msg);

            _fixture.RepositoryEvents.WaitFor<UserMsgs.AuthenticationFailedAccountDisabled>(TimeSpan.FromMilliseconds(100));

            _fixture
                .TestQueue
                .AssertNext<IdentityMsgs.UserAuthenticationFailedAccountDisabled>(_command.CorrelationId)
                .AssertEmpty();

            _fixture
                .RepositoryEvents
                .AssertNext<UserMsgs.UserCreated>(_command.CorrelationId, out var created)
                .AssertNext<UserMsgs.AuthenticationFailedAccountDisabled>(_command.CorrelationId, out var failed)
                .AssertEmpty();

            Assert.Equal(AuthProvider, created.AuthProvider);
            Assert.Equal(AuthDomain, created.AuthDomain);
            Assert.Equal(UserName, created.UserName);
            Assert.Equal(created.Id, failed.Id);
            Assert.Equal(HostIPAddress, failed.HostIPAddress);
        }

        [Fact]
        public void can_log_unknown_user_invalid_credentials()
        {
            var msg = MessageBuilder
                        .From(_command)
                        .Build(() => new IdentityMsgs.UserAuthenticationFailedInvalidCredentials(
                                            AuthProvider,
                                            AuthDomain,
                                            UserName,
                                            HostIPAddress));
            _fixture.Dispatcher.Publish(msg);

            _fixture.RepositoryEvents.WaitFor<UserMsgs.AuthenticationFailedInvalidCredentials>(TimeSpan.FromMilliseconds(100));

            _fixture
                .TestQueue
                .AssertNext<IdentityMsgs.UserAuthenticationFailedInvalidCredentials>(_command.CorrelationId)
                .AssertEmpty();

            _fixture
                .RepositoryEvents
                .AssertNext<UserMsgs.UserCreated>(_command.CorrelationId, out var created)
                .AssertNext<UserMsgs.AuthenticationFailedInvalidCredentials>(_command.CorrelationId, out var failed)
                .AssertEmpty();

            Assert.Equal(AuthProvider, created.AuthProvider);
            Assert.Equal(AuthDomain, created.AuthDomain);
            Assert.Equal(UserName, created.UserName);
            Assert.Equal(created.Id, failed.Id);
            Assert.Equal(HostIPAddress, failed.HostIPAddress);
        }

        [Fact]
        public void can_log_unknown_user_authentication_failed_by_external_provider()
        {
            var msg = MessageBuilder
                        .From(_command)
                        .Build(() => new IdentityMsgs.UserAuthenticationFailedByExternalProvider(
                                            AuthProvider,
                                            HostIPAddress));
            _fixture.Dispatcher.Publish(msg);

            _fixture.RepositoryEvents.WaitFor<ExternalProviderMsgs.AuthenticationFailedInvalidCredentials>(TimeSpan.FromMilliseconds(100));

            _fixture
                .TestQueue
                .AssertNext<IdentityMsgs.UserAuthenticationFailedByExternalProvider>(_command.CorrelationId)
                .AssertEmpty();

            _fixture
                .RepositoryEvents
                .AssertNext<ExternalProviderMsgs.ProviderCreated>(_command.CorrelationId)
                .AssertNext<ExternalProviderMsgs.AuthenticationFailedInvalidCredentials>(_command.CorrelationId)
                .AssertEmpty();
        }

        [Fact]
        public void second_failed_authentication_from_external_provider_uses_existing_stream()
        {
            var msg = MessageBuilder
                        .From(_command)
                        .Build(() => new IdentityMsgs.UserAuthenticationFailedByExternalProvider(
                                            AuthProvider,
                                            HostIPAddress));
            _fixture.Dispatcher.Publish(msg);

            _fixture.RepositoryEvents.WaitFor<ExternalProviderMsgs.AuthenticationFailedInvalidCredentials>(TimeSpan.FromMilliseconds(100));

            var created = _fixture.RepositoryEvents.DequeueNext<ExternalProviderMsgs.ProviderCreated>();
            _fixture.ClearQueues();

            var msg2 = MessageBuilder
                        .New(() => new IdentityMsgs.UserAuthenticationFailedByExternalProvider(
                                            AuthProvider,
                                            HostIPAddress));
            _fixture.Dispatcher.Publish(msg2);

            _fixture.RepositoryEvents.WaitFor<ExternalProviderMsgs.AuthenticationFailedInvalidCredentials>(TimeSpan.FromMilliseconds(100));

            _fixture
                .TestQueue
                .AssertNext<IdentityMsgs.UserAuthenticationFailedByExternalProvider>(msg2.CorrelationId)
                .AssertEmpty();

            _fixture
                .RepositoryEvents
                .AssertNext<ExternalProviderMsgs.AuthenticationFailedInvalidCredentials>(msg2.CorrelationId, out var persistedEvent)
                .AssertEmpty();

            Assert.Equal(created.ProviderId, persistedEvent.ProviderId);
        }

        [Fact]
        public void can_log_authentication_failed()
        {
            AddUser();
            _fixture.Dispatcher.Publish(
                MessageBuilder
                    .From(_command)
                    .Build(() => new IdentityMsgs.UserAuthenticationFailed(
                                        AuthProvider,
                                        AuthDomain,
                                        UserName,
                                        AuthenticationError,
                                        HostIPAddress)));
            _fixture.RepositoryEvents.WaitFor<UserMsgs.AuthenticationFailed>(TimeSpan.FromMilliseconds(100));
            _fixture
                .TestQueue
                .AssertNext<IdentityMsgs.UserAuthenticationFailed>(_command.CorrelationId)
                .AssertEmpty();
            _fixture
                .RepositoryEvents
                .AssertNext<UserMsgs.AuthenticationFailed>(_command.CorrelationId)
                .AssertEmpty();

        }

        [Fact]
        public void can_log_authentication_failed_account_locked()
        {
            AddUser();
            _fixture.Dispatcher.Publish(
                MessageBuilder
                    .From(_command)
                    .Build(() => new IdentityMsgs.UserAuthenticationFailedAccountLocked(
                                        AuthProvider,
                                        AuthDomain,
                                        UserName,
                                        HostIPAddress
                                        )));
            _fixture.RepositoryEvents.WaitFor<UserMsgs.AuthenticationFailedAccountLocked>(TimeSpan.FromMilliseconds(100));
            _fixture
                .TestQueue
                .AssertNext<IdentityMsgs.UserAuthenticationFailedAccountLocked>(_command.CorrelationId)
                .AssertEmpty();
            _fixture
                .RepositoryEvents
                .AssertNext<UserMsgs.AuthenticationFailedAccountLocked>(_command.CorrelationId)
                .AssertEmpty();
        }

        [Fact]
        public void can_log_authentication_failed_account_disabled()
        {
            AddUser();
            _fixture.Dispatcher.Publish(
                MessageBuilder
                    .From(_command)
                    .Build(() => new IdentityMsgs.UserAuthenticationFailedAccountDisabled(
                                        AuthProvider,
                                        AuthDomain,
                                        UserName,
                                        HostIPAddress
                                        )));
            _fixture.RepositoryEvents.WaitFor<UserMsgs.AuthenticationFailedAccountDisabled>(TimeSpan.FromMilliseconds(100));
            _fixture
                .TestQueue
                .AssertNext<IdentityMsgs.UserAuthenticationFailedAccountDisabled>(_command.CorrelationId)
                .AssertEmpty();
            _fixture
                .RepositoryEvents
                .AssertNext<UserMsgs.AuthenticationFailedAccountDisabled>(_command.CorrelationId)
                .AssertEmpty();
        }

        [Fact]
        public void can_log_authentication_failed_invalid_credentials()
        {
            AddUser();
            _fixture.Dispatcher.Publish(
                MessageBuilder
                    .From(_command)
                    .Build(() => new IdentityMsgs.UserAuthenticationFailedInvalidCredentials(
                                        AuthProvider,
                                        AuthDomain,
                                        UserName,
                                        HostIPAddress
                                        )));
            _fixture.RepositoryEvents.WaitFor<UserMsgs.AuthenticationFailedInvalidCredentials>(TimeSpan.FromMilliseconds(100));
            _fixture
                .TestQueue
                .AssertNext<IdentityMsgs.UserAuthenticationFailedInvalidCredentials>(_command.CorrelationId)
                .AssertEmpty();
            _fixture
                .RepositoryEvents
                .AssertNext<UserMsgs.AuthenticationFailedInvalidCredentials>(_command.CorrelationId)
                .AssertEmpty();
        }

        [Fact]
        public void can_log_authentication_failed_by_external_provider()
        {
            _fixture.Dispatcher.Publish(
                MessageBuilder
                    .From(_command)
                    .Build(() => new IdentityMsgs.UserAuthenticationFailedByExternalProvider(
                                        AuthProvider,
                                        HostIPAddress
                                        )));
            _fixture.TestQueue.WaitFor<IdentityMsgs.UserAuthenticationFailedByExternalProvider>(TimeSpan.FromMilliseconds(100));
            _fixture
                .TestQueue
                .AssertNext<IdentityMsgs.UserAuthenticationFailedByExternalProvider>(_command.CorrelationId)
                .AssertEmpty();
        }

        [Fact]
        public void can_assign_role_to_user()
        {
            CreateUser();
            CreateRole();

            var cmd = MessageBuilder.New(
                () => new UserMsgs.AssignRole(_userId, _roleId));
            _fixture.Dispatcher.Send(cmd);

            _fixture.RepositoryEvents.WaitFor<UserMsgs.RoleAssigned>(TimeSpan.FromMilliseconds(100));
            _fixture
                .TestQueue
                .AssertNext<UserMsgs.AssignRole>(cmd.CorrelationId)
                .AssertEmpty();
            _fixture
                .RepositoryEvents
                .AssertNext<UserMsgs.RoleAssigned>(cmd.CorrelationId)
                .AssertEmpty();

        }

        [Fact]
        public void can_unassign_role_from_user()
        {
            CreateUser();
            CreateRole();
            AssignRole();

            var cmd = MessageBuilder.New(
                () => new UserMsgs.UnassignRole(_userId, _roleId));
            _fixture.Dispatcher.Send(cmd);

            _fixture.RepositoryEvents.WaitFor<UserMsgs.RoleUnassigned>(TimeSpan.FromMilliseconds(100));
            _fixture
                .TestQueue
                .AssertNext<UserMsgs.UnassignRole>(cmd.CorrelationId)
                .AssertEmpty();
            _fixture
                .RepositoryEvents
                .AssertNext<UserMsgs.RoleUnassigned>(cmd.CorrelationId)
                .AssertEmpty();

        }

        [Fact]
        public void can_deactivate_user()
        {
            CreateUser();

            var cmd = MessageBuilder.New(
                () => new UserMsgs.Deactivate(_userId));
            _fixture.Dispatcher.Send(cmd);

            _fixture.RepositoryEvents.WaitFor<UserMsgs.Deactivated>(TimeSpan.FromMilliseconds(100));
            _fixture
                .TestQueue
                .AssertNext<UserMsgs.Deactivate>(cmd.CorrelationId)
                .AssertEmpty();
            _fixture
                .RepositoryEvents
                .AssertNext<UserMsgs.Deactivated>(cmd.CorrelationId)
                .AssertEmpty();

        }

        [Fact]
        public void can_activate_user()
        {
            CreateUser();

            var cmd = MessageBuilder.New(
                () => new UserMsgs.Activate(_userId));
            _fixture.Dispatcher.Send(cmd);

            _fixture.RepositoryEvents.WaitFor<UserMsgs.Activated>(TimeSpan.FromMilliseconds(100));
            _fixture
                .TestQueue
                .AssertNext<UserMsgs.Activate>(cmd.CorrelationId)
                .AssertEmpty();
            _fixture
                .RepositoryEvents
                .AssertNext<UserMsgs.Activated>(cmd.CorrelationId)
                .AssertEmpty();

        }
        [Fact]
        public void can_update_given_name()
        {
            CreateUser();

            var cmd = MessageBuilder.New(
                () => new UserMsgs.UpdateGivenName(_userId, GiveNameUpdate));
            _fixture.Dispatcher.Send(cmd);

            _fixture.RepositoryEvents.WaitFor<UserMsgs.GivenNameUpdated>(TimeSpan.FromMilliseconds(100));
            _fixture
                .TestQueue
                .AssertNext<UserMsgs.UpdateGivenName>(cmd.CorrelationId)
                .AssertEmpty();
            _fixture
                .RepositoryEvents
                .AssertNext<UserMsgs.GivenNameUpdated>(cmd.CorrelationId)
                .AssertEmpty();

        }
        [Fact]
        public void cannot_update_given_name_without_user()
        {
            var cmd = MessageBuilder.New(
                () => new UserMsgs.UpdateGivenName(_userId, GiveNameUpdate));
            AssertEx.CommandThrows<AggregateNotFoundException>(
                () => _fixture.Dispatcher.Send(cmd));
        }
        [Fact]
        public void can_update_surname()
        {
            CreateUser();

            var cmd = MessageBuilder.New(
                () => new UserMsgs.UpdateSurname(_userId, SurnameUpdate));
            _fixture.Dispatcher.Send(cmd);

            _fixture.RepositoryEvents.WaitFor<UserMsgs.SurnameUpdated>(TimeSpan.FromMilliseconds(100));
            _fixture
                .TestQueue
                .AssertNext<UserMsgs.UpdateSurname>(cmd.CorrelationId)
                .AssertEmpty();
            _fixture
                .RepositoryEvents
                .AssertNext<UserMsgs.SurnameUpdated>(cmd.CorrelationId)
                .AssertEmpty();

        }
        [Fact]
        public void cannot_update_surname_without_user()
        {
            var cmd = MessageBuilder.New(
                () => new UserMsgs.UpdateSurname(_userId, SurnameUpdate));
            AssertEx.CommandThrows<AggregateNotFoundException>(
                () => _fixture.Dispatcher.Send(cmd));
        }
        [Fact]
        public void can_update_fullname()
        {
            CreateUser();

            var cmd = MessageBuilder.New(
                () => new UserMsgs.UpdateFullName(_userId, FullNameUpdate));
            _fixture.Dispatcher.Send(cmd);

            _fixture.RepositoryEvents.WaitFor<UserMsgs.FullNameUpdated>(TimeSpan.FromMilliseconds(100));
            _fixture
                .TestQueue
                .AssertNext<UserMsgs.UpdateFullName>(cmd.CorrelationId)
                .AssertEmpty();
            _fixture
                .RepositoryEvents
                .AssertNext<UserMsgs.FullNameUpdated>(cmd.CorrelationId)
                .AssertEmpty();

        }
        [Fact]
        public void cannot_update_full_name_without_user()
        {
            var cmd = MessageBuilder.New(
                () => new UserMsgs.UpdateFullName(_userId, FullNameUpdate));
            AssertEx.CommandThrows<AggregateNotFoundException>(
                () => _fixture.Dispatcher.Send(cmd));
        }
        [Fact]
        public void can_update_email()
        {
            CreateUser();

            var cmd = MessageBuilder.New(
                () => new UserMsgs.UpdateEmail(_userId, EmailUpdate));
            _fixture.Dispatcher.Send(cmd);

            _fixture.RepositoryEvents.WaitFor<UserMsgs.EmailUpdated>(TimeSpan.FromMilliseconds(100));
            _fixture
                .TestQueue
                .AssertNext<UserMsgs.UpdateEmail>(cmd.CorrelationId)
                .AssertEmpty();
            _fixture
                .RepositoryEvents
                .AssertNext<UserMsgs.EmailUpdated>(cmd.CorrelationId)
                .AssertEmpty();

        }
        [Fact]
        public void cannot_updateEmail_without_user()
        {
            var cmd = MessageBuilder.New(
                () => new UserMsgs.UpdateEmail(_userId, EmailUpdate));
            AssertEx.CommandThrows<AggregateNotFoundException>(
                () => _fixture.Dispatcher.Send(cmd));
        }

        [Fact]
        public void can_update_auth_domain()
        {
            CreateUser();

            var cmd = MessageBuilder.New(
                () => new UserMsgs.UpdateAuthDomain(_userId, AuthDomainUpdate));
            _fixture.Dispatcher.Send(cmd);

            _fixture.RepositoryEvents.WaitFor<UserMsgs.AuthDomainUpdated>(TimeSpan.FromMilliseconds(100));
            _fixture
                .TestQueue
                .AssertNext<UserMsgs.UpdateAuthDomain>(cmd.CorrelationId)
                .AssertEmpty();
            _fixture
                .RepositoryEvents
                .AssertNext<UserMsgs.AuthDomainUpdated>(cmd.CorrelationId)
                .AssertEmpty();

        }
        [Fact]
        public void cannot_update_auth_domain_without_user()
        {
            var cmd = MessageBuilder.New(
                () => new UserMsgs.UpdateAuthDomain(_userId, AuthDomainUpdate));
            AssertEx.CommandThrows<AggregateNotFoundException>(
                () => _fixture.Dispatcher.Send(cmd));
        }
        [Fact]
        public void can_update_username()
        {
            CreateUser();

            var cmd = MessageBuilder.New(
                () => new UserMsgs.UpdateUserName(_userId, UserNameUpdate));
            _fixture.Dispatcher.Send(cmd);

            _fixture.RepositoryEvents.WaitFor<UserMsgs.UserNameUpdated>(TimeSpan.FromMilliseconds(100));
            _fixture
                .TestQueue
                .AssertNext<UserMsgs.UpdateUserName>(cmd.CorrelationId)
                .AssertEmpty();
            _fixture
                .RepositoryEvents
                .AssertNext<UserMsgs.UserNameUpdated>(cmd.CorrelationId)
                .AssertEmpty();

        }
        [Fact]
        public void cannot_update_username_without_user()
        {
            var cmd = MessageBuilder.New(
                () => new UserMsgs.UpdateUserName(_userId, UserNameUpdate));
            AssertEx.CommandThrows<AggregateNotFoundException>(
                () => _fixture.Dispatcher.Send(cmd));
        }
        private void CreateUser()
        {
            var cmd = MessageBuilder.New(
                () => new UserMsgs.CreateUser(
                                    _userId,
                                    _userSidFromAuthProvider,
                                    AuthProvider,
                                    AuthDomain,
                                    UserName,
                                    FullName,
                                    GivenName,
                                    Surname,
                                    Email));
            _fixture.Dispatcher.Send(cmd);
            _fixture.RepositoryEvents.WaitFor<UserMsgs.UserCreated>(TimeSpan.FromMilliseconds(100));
            _fixture.ClearQueues();

        }

        private void CreateRole()
        {
            var cmd = MessageBuilder.New(
                () => new RoleMsgs.CreateRole(
                                    _roleId,
                                    RoleName,
                                    Application));
            _fixture.Dispatcher.Send(cmd);
            _fixture.RepositoryEvents.WaitFor<RoleMsgs.RoleCreated>(TimeSpan.FromMilliseconds(100));
            _fixture.ClearQueues();

        }

        private void AddUser()
        {
            var evt = MessageBuilder.New(
                () => new UserMsgs.UserCreated(
                    _userId,
                    _userSidFromAuthProvider,
                    AuthProvider,
                    AuthDomain,
                    UserName,
                    FullName,
                    GivenName,
                    Surname,
                    Email));
            var stream = _fixture.StreamNameBuilder.GenerateForAggregate(typeof(User), _userId);
            _fixture.StreamStoreConnection.AppendToStream(
                stream,
                ExpectedVersion.Any,
                null,
                _fixture.EventSerializer.Serialize(evt));
            _fixture.RepositoryEvents.WaitFor<UserMsgs.UserCreated>(TimeSpan.FromMilliseconds(100));
            _fixture.ClearQueues();
        }

        private void AssignRole()
        {
            var evt = MessageBuilder.New(
                () => new UserMsgs.RoleAssigned(_userId, _roleId));
            var stream = _fixture.StreamNameBuilder.GenerateForAggregate(typeof(User), _userId);
            _fixture.StreamStoreConnection.AppendToStream(
                stream,
                ExpectedVersion.Any,
                null,
                _fixture.EventSerializer.Serialize(evt));
            _fixture.RepositoryEvents.WaitFor<UserMsgs.RoleAssigned>(TimeSpan.FromMilliseconds(100));
            _fixture.ClearQueues();

        }
    }

}

