using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ReactiveDomain.Identity.Tests
{
    public class SubjectSvcTests
    {
        /*
        private readonly ICorrelatedMessage _command = MessageBuilder.New(() => new TestMessages.RootCommand());

        private readonly MockRepositorySpecification _fixture;
        private readonly UserSvc _userSvc;
        private readonly PolicySvc _policySvc;

        private readonly Guid _userId = Guid.NewGuid();
        private readonly string _userSidFromAuthProvider = Guid.NewGuid().ToString();
        private const string AuthProvider = Constants.AuthenticationProviderAD;
        private const string AuthDomain = "Perkinelmer";
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
        private const string Application = "TestApp1";
        private const string HostIPAddress = "127.0.0.1";
         * 
        [Fact]
        public void cannot_create_duplicate_user()
        {
            AddUser();
            var cmd = MessageBuilder.New(
                        () => new UserMsgs.CreateUser(
                                    _userId,
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
        */
    }
}
