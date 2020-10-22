using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Testing;
using ReactiveDomain.Users.Domain.Aggregates;
using ReactiveDomain.Users.Domain.Services;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Users.Tests.Helpers;
using Xunit;

namespace ReactiveDomain.Users.Tests
{
    [Collection("UserDomainTests")]
    public sealed class UsersRMTests : IDisposable
    {
        private readonly MockRepositorySpecification _fixture;
        private readonly UsersRM _rm;

        private readonly Guid _id1 = Guid.NewGuid();
        private readonly Guid _id2 = Guid.NewGuid();
        private readonly string SubjectId = Guid.NewGuid().ToString();
        private readonly string SubjectId2 = Guid.NewGuid().ToString();
        private const string AuthProvider = Constants.AuthenticationProviderAD;
        private const string AuthDomain = "Perkinelmernet";
        private const string UserName = "jsmith";
        private const string UserName2 = "jsmith2";
        private const string GivenName = "John";
        private const string Surname = "Smith";
        private const string FullName = "John Smith";
        private const string Email = "john.smith@perkinelmer.com";

        public UsersRMTests()
        {
            _fixture = new MockRepositorySpecification();
            _rm = new UsersRM(()=>_fixture.GetListener(nameof(UsersRMTests)));
            AddUsers();
        }

        public void Dispose()
        {
            _rm?.Dispose();
        }

        [Fact]
        public void correct_users_exist()
        {
            Assert.True(_rm.UserExists(_id1));
            Assert.True(_rm.UserExists(AuthProvider, AuthDomain, SubjectId, out _));
            Assert.True(_rm.UserExists(_id2));
            Assert.True(_rm.UserExists(AuthProvider, AuthDomain, SubjectId2, out _));

            Assert.False(_rm.UserExists(Guid.NewGuid()));
            Assert.False(_rm.UserExists(Guid.Empty));
            Assert.False(_rm.UserExists(AuthProvider, AuthDomain, "bogus", out _));
            Assert.False(_rm.UserExists(AuthProvider, "bogus", SubjectId, out _));
            Assert.False(_rm.UserExists("bogus", AuthDomain, SubjectId, out _));
        }

        [Fact]
        public void can_get_users_by_name()
        {
            Assert.True(_rm.TryGetUserId(AuthProvider, AuthDomain, SubjectId,  out var id1));
            Assert.Equal(_id1, id1);
            Assert.True(_rm.TryGetUserId(AuthProvider, AuthDomain, SubjectId2,  out var id2));
            Assert.Equal(_id2, id2);
        }
        [Fact]
        public void can_get_users_by_userSidFromAuthProvider()
        {
            Assert.True(_rm.TryGetUserId(AuthProvider, AuthDomain, SubjectId, out var id1));
            Assert.Equal(_id1, id1);
            Assert.True(_rm.TryGetUserId(AuthProvider, AuthDomain, SubjectId2, out var id2));
            Assert.Equal(_id2, id2);
        }
        [Fact]
        public void cannot_get_nonexistent_user()
        {
            Assert.False(_rm.TryGetUserId("bogus", AuthDomain, UserName,  out _));
            Assert.False(_rm.TryGetUserId(AuthProvider, "bogus", UserName,  out _));
            Assert.False(_rm.TryGetUserId(AuthProvider, AuthDomain, "bogus",  out _));
        }

        private void AddUsers()
        {
            var evt1 = MessageBuilder.New(
                () => new UserMsgs.UserCreated(
                            _id1,
                            SubjectId,
                            AuthProvider,
                            AuthDomain,
                            UserName,
                            FullName,
                            GivenName,
                            Surname,
                            Email));
            var evt2 = MessageBuilder.New(
                () => new UserMsgs.UserCreated(
                            _id2,
                            SubjectId2,
                            AuthProvider,
                            AuthDomain,
                            UserName2,
                            FullName,
                            GivenName,
                            Surname,
                            Email));
            var stream1 = _fixture.StreamNameBuilder.GenerateForAggregate(typeof(User), _id1);
            var stream2 = _fixture.StreamNameBuilder.GenerateForAggregate(typeof(User), _id2);
            _fixture.StreamStoreConnection.AppendToStream(
                stream1,
                ExpectedVersion.Any,
                null,
                _fixture.EventSerializer.Serialize(evt1));
            _fixture.RepositoryEvents.WaitForMsgId(evt1.MsgId, TimeSpan.FromMilliseconds(100));
            _fixture.StreamStoreConnection.AppendToStream(
                stream2,
                ExpectedVersion.Any,
                null,
                _fixture.EventSerializer.Serialize(evt2));
            _fixture.RepositoryEvents.WaitForMsgId(evt2.MsgId, TimeSpan.FromMilliseconds(100));
            _fixture.ClearQueues();
        }
    }
}
