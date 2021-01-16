using System;
using System.Linq;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Testing;
using ReactiveDomain.Users.Domain.Aggregates;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Users.Tests.Helpers;
using Xunit;

namespace ReactiveDomain.Users.Tests
{
    public class UserEntitlementRMTests : IDisposable
    {
        private readonly MockRepositorySpecification _fixture;
        private readonly UserEntitlementRM _rm;

        private readonly Guid _userId = Guid.NewGuid();
        private readonly string _userSidFromAuthProvider = Guid.NewGuid().ToString();
        private const string AuthProvider = Constants.AuthenticationProviderAD;
        private const string AuthDomain = "Perkinelmernet";
        private const string UserName = "jsmith";
        private const string GivenName = "John";
        private const string Surname = "Smith";
        private const string FullName = "John Smith";
        private const string Email = "john.smith@perkinelmer.com";
        private const string UnknownUserSidFromAuthProvider = "Unknown";

        private readonly Guid _roleId = Guid.NewGuid();
        private const string RoleName = "Admin";
        private const string Application = "Kaleido";

        private IStreamNameBuilder _streamNamer = new PrefixedCamelCaseStreamNameBuilder(nameof(UserEntitlementRMTests));

        public UserEntitlementRMTests()
        {
            _fixture = new MockRepositorySpecification();
           

            _rm = new UserEntitlementRM((name) => new StreamListener(name, _fixture.StreamStoreConnection, _streamNamer, new JsonMessageSerializer()));
            CreateUser();
            CreateRole();
        }


        /// <summary>
        /// Cleanup RM
        /// </summary>
        public void Dispose()
        {
            _rm?.Dispose();
        }

        [Fact]
        public void user_assigned_to_role()
        {
            AssignRole();
            var roles = _rm.RolesForUser(
                                _userSidFromAuthProvider,
                                UserName,
                                AuthDomain,
                                Application);
            Assert.Contains(roles, r => r.Name == RoleName && r.Application == Application);
        }

        [Fact]
        public void user_unassigned_from_role()
        {
            AssignRole();
            UnassignRole();
            var roles = _rm.RolesForUser(
                                _userSidFromAuthProvider,
                                UserName,
                                AuthDomain,
                                Application);
            Assert.DoesNotContain(roles, r => r.Name == RoleName && r.Application == Application);
        }

        [Fact]
        public void no_roles_for_unknown_user()
        {
            Assert.Throws<UserNotFoundException>(() => _rm.RolesForUser(
                                                            UnknownUserSidFromAuthProvider,
                                                            UserName,
                                                                    AuthDomain,
                                                                    Application));
        }

        [Fact]
        public void no_roles_for_deactivated_user()
        {
            DeactivateUser();
            Assert.Throws<UserDeactivatedException>(() => _rm.RolesForUser(
                                                                        _userSidFromAuthProvider,
                                                                        UserName,
                                                                        AuthDomain,
                                                                        Application));
        }

        private void CreateUser()
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
            var stream = _streamNamer.GenerateForAggregate(typeof(User), _userId);
            _fixture.StreamStoreConnection.AppendToStream(
                                            stream,
                                            ExpectedVersion.Any,
                                            null,
                                            _fixture.EventSerializer.Serialize(evt));
            _fixture.RepositoryEvents.WaitForMsgId(evt.MsgId, TimeSpan.FromMilliseconds(100));
            _fixture.ClearQueues();
        }

        private void CreateRole()
        {
            var evt = MessageBuilder.New(
                () => new RoleMsgs.RoleCreated(
                    _roleId,
                    RoleName,
                    Application));
            var stream = _streamNamer.GenerateForAggregate(typeof(Role), _roleId);
            _fixture.StreamStoreConnection.AppendToStream(
                                            stream,
                                            ExpectedVersion.Any,
                                            null,
                                            _fixture.EventSerializer.Serialize(evt));
            _fixture.RepositoryEvents.WaitForMsgId(evt.MsgId, TimeSpan.FromMilliseconds(100));
            _fixture.ClearQueues();
        }

        private void AssignRole()
        {
            var evt = MessageBuilder.New(
                () => new UserMsgs.RoleAssigned(_userId, _roleId));

            var stream = _streamNamer.GenerateForAggregate(typeof(User), _userId);
            _fixture.StreamStoreConnection.AppendToStream(
                                            stream,
                                            ExpectedVersion.Any,
                                            null,
                                            _fixture.EventSerializer.Serialize(evt));
            _fixture.RepositoryEvents.WaitForMsgId(evt.MsgId, TimeSpan.FromMilliseconds(100));
            AssertEx.IsOrBecomesTrue(() => _rm.ActivatedUsers.Any(x => x.Roles.Any(r => r.RoleId == _roleId)), 100);
            _fixture.ClearQueues();

        }
        private void UnassignRole()
        {
            var evt = MessageBuilder.New(
                () => new UserMsgs.RoleUnassigned(_userId, _roleId));

            var stream = _streamNamer.GenerateForAggregate(typeof(User), _userId);
            _fixture.StreamStoreConnection.AppendToStream(
                                            stream,
                                            ExpectedVersion.Any,
                                            null,
                                            _fixture.EventSerializer.Serialize(evt));
            _fixture.RepositoryEvents.WaitForMsgId(evt.MsgId, TimeSpan.FromMilliseconds(100));
            AssertEx.IsOrBecomesFalse(() => _rm.ActivatedUsers.Any(x => x.Roles.Any(r => r.RoleId == _roleId)));
            _fixture.ClearQueues();
        }

        private void DeactivateUser()
        {
            var evt = MessageBuilder.New(
                () => new UserMsgs.Deactivated(_userId));

            var stream = _streamNamer.GenerateForAggregate(typeof(User), _userId);
            _fixture.StreamStoreConnection.AppendToStream(
                stream,
                ExpectedVersion.Any,
                null,
                _fixture.EventSerializer.Serialize(evt));
            _fixture.RepositoryEvents.WaitForMsgId(evt.MsgId, TimeSpan.FromMilliseconds(100));
            AssertEx.IsOrBecomesFalse(() => _rm.ActivatedUsers.Any(x => x.UserName == UserName));
            _fixture.ClearQueues();
        }


    }
}
