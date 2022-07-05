using System;
using ReactiveDomain.IdentityStorage.Domain;
using ReactiveDomain.IdentityStorage.Messages;
using ReactiveDomain.IdentityStorage.ReadModels;
using ReactiveDomain.Messaging;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.IdentityStorage.Tests
{
    [Collection("UserDomainTests")]
    public sealed class UsersRMTests : IDisposable
    {
        private readonly MockRepositorySpecification _fixture;
        private readonly UsersRm _rm;

        private readonly Guid _id1 = Guid.NewGuid();
        private readonly Guid _id2 = Guid.NewGuid();
        private readonly string SubjectId = Guid.NewGuid().ToString();
        private readonly string SubjectId2 = Guid.NewGuid().ToString();
        private const string AuthProvider = "AD";
        private const string AuthDomain = "CompanyNet";
        private const string UserName = "jsmith";
        private const string UserName2 = "jsmith2";
        private const string GivenName = "John";
        private const string Surname = "Smith";
        private const string FullName = "John Smith";
        private const string Email = "john.smith@Company1.com";

        public UsersRMTests()
        {
            _fixture = new MockRepositorySpecification();
            AddUsers();
            _rm = new UsersRm(_fixture.ConfiguredConnection);

        }



        [Fact]
        public void correct_users_exist()
        {
            Assert.True(_rm.UsersById.ContainsKey(_id1));
            Assert.True(_rm.HasUser(SubjectId, AuthDomain, out _));
            Assert.True(_rm.UsersById.ContainsKey(_id2));
            Assert.True(_rm.HasUser(SubjectId2, AuthDomain, out _));

            Assert.False(_rm.UsersById.ContainsKey(Guid.NewGuid()));
            Assert.False(_rm.UsersById.ContainsKey(Guid.Empty));
            Assert.False(_rm.HasUser(AuthDomain, "bogus", out _));
            Assert.False(_rm.HasUser("bogus", SubjectId, out _));

        }

        [Fact]
        public void can_get_user_id_by_SID()
        {
            Assert.True(_rm.HasUser(SubjectId, AuthDomain, out var id1));
            Assert.Equal(_id1, id1);
            Assert.True(_rm.HasUser(SubjectId2, AuthDomain, out var id2));
            Assert.Equal(_id2, id2);
        }


        [Fact]
        public void cannot_get_nonexistent_user()
        {

            Assert.False(_rm.HasUser(SubjectId, "bogus", out _));
            Assert.False(_rm.HasUser("bogus", AuthDomain, out _));
        }

        private void AddUsers()
        {
            var evt1 = MessageBuilder.New(
                () => new UserMsgs.UserCreated(
                            _id1));
            var evt2 = MessageBuilder.New(
               () => new UserMsgs.UserDetailsUpdated(
                           _id1,
                            FullName,
                            GivenName,
                            Surname,
                            Email));
            var evt3 = MessageBuilder.New(
               () => new UserMsgs.AuthDomainMapped(
                           _id1,
                            SubjectId,
                            AuthProvider,
                            AuthDomain,
                            UserName));

            var evt4 = MessageBuilder.New(
                           () => new UserMsgs.UserCreated(
                                       _id2));
            var evt5 = MessageBuilder.New(
               () => new UserMsgs.UserDetailsUpdated(
                           _id2,
                            FullName,
                            GivenName,
                            Surname,
                            Email));
            var evt6 = MessageBuilder.New(
               () => new UserMsgs.AuthDomainMapped(
                           _id2,
                            SubjectId2,
                            AuthProvider,
                            AuthDomain,
                            UserName2));


            var stream1 = _fixture.StreamNameBuilder.GenerateForAggregate(typeof(User), _id1);
            var stream2 = _fixture.StreamNameBuilder.GenerateForAggregate(typeof(User), _id2);
            _fixture.StreamStoreConnection.AppendToStream(
                stream1,
                ExpectedVersion.Any,
                null,
                new[] { _fixture.EventSerializer.Serialize(evt1), _fixture.EventSerializer.Serialize(evt2), _fixture.EventSerializer.Serialize(evt3) });
            _fixture.RepositoryEvents.WaitForMsgId(evt3.MsgId, TimeSpan.FromMilliseconds(100));
            _fixture.StreamStoreConnection.AppendToStream(
                stream2,
                ExpectedVersion.Any,
                null,
                new[] { _fixture.EventSerializer.Serialize(evt4), _fixture.EventSerializer.Serialize(evt5), _fixture.EventSerializer.Serialize(evt6) });
            _fixture.RepositoryEvents.WaitForMsgId(evt6.MsgId, TimeSpan.FromMilliseconds(100));
            _fixture.ClearQueues();
        }
        public void Dispose()
        {
            _rm?.Dispose();
        }
    }
}
