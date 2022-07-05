using System;
using ReactiveDomain.Foundation;
using ReactiveDomain.IdentityStorage.Domain;
using ReactiveDomain.IdentityStorage.Messages;
using ReactiveDomain.IdentityStorage.Services;
using ReactiveDomain.Messaging;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.IdentityStorage.Tests
{
    [Collection("UserDomainTests")]
    public sealed class UserServiceTests :
        IDisposable
    {
        private readonly ICorrelatedMessage _command = MessageBuilder.New(() => new TestMessages.RootCommand());

        private readonly MockRepositorySpecification _fixture;
        private readonly UserSvc _userSvc;

        private readonly Guid _userId = Guid.NewGuid();
        private const string GivenName = "John";
        private const string Surname = "Smith";
        private const string FullName = "John Smith";
        private const string Email = "john.smith@Company1.com";
        private const string GiveNameUpdate = "John Update";
        private const string SurnameUpdate = "Smith Update";
        private const string FullNameUpdate = "John Smith Update";
        private const string EmailUpdate = "john.smithUpdate@Company1.com";
        private const string ClientScope = "APPLICATION1";

        public UserServiceTests()
        {
            _fixture = new MockRepositorySpecification();
            _userSvc = new UserSvc(_fixture.Repository, _fixture.Dispatcher);
        }



        [Fact]
        public void can_create_new_user()
        {
            var cmd = MessageBuilder.New(
                        () => new UserMsgs.CreateUser(
                                    _userId,
                                    FullName,
                                    GivenName,
                                    Surname,
                                    Email));
            _fixture.Dispatcher.Send(cmd);

            _fixture.RepositoryEvents.WaitFor<UserMsgs.UserDetailsUpdated>(TimeSpan.FromMilliseconds(100));
            _fixture
                .TestQueue
                .AssertNext<UserMsgs.CreateUser>(cmd.CorrelationId)
                .AssertEmpty();
            _fixture
                .RepositoryEvents
                .AssertNext<UserMsgs.UserCreated>(cmd.CorrelationId)
                .AssertNext<UserMsgs.Activated>(cmd.CorrelationId)
                .AssertNext<UserMsgs.UserDetailsUpdated>(cmd.CorrelationId)
                .AssertEmpty();
        }

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
        public void can_add_client_scope_to_user()
        {
            AddUser();

            var cmd = MessageBuilder.New(
                () => new UserMsgs.AddClientScope(_userId, ClientScope));
            _fixture.Dispatcher.Send(cmd);

            _fixture.RepositoryEvents.WaitFor<UserMsgs.ClientScopeAdded>(TimeSpan.FromMilliseconds(100));
            _fixture
                .TestQueue
                .AssertNext<UserMsgs.AddClientScope>(cmd.CorrelationId)
                .AssertEmpty();
            _fixture
                .RepositoryEvents
                .AssertNext<UserMsgs.ClientScopeAdded>(cmd.CorrelationId, out UserMsgs.ClientScopeAdded evt)
                .AssertEmpty();
            Assert.Equal(ClientScope, evt.ClientScope);
        }

        [Fact]
        public void can_remove_client_scope_from_user()
        {
            //given
            AddUser();
            var givenEvt = _fixture.EventSerializer.Serialize(MessageBuilder.New(
              () => new UserMsgs.ClientScopeAdded(
                  _userId,ClientScope)));

            var stream = _fixture.StreamNameBuilder.GenerateForAggregate(typeof(User), _userId);
            _fixture.StreamStoreConnection.AppendToStream(
                stream,
                ExpectedVersion.Any,
                null,
                new[] { givenEvt });
            _fixture.RepositoryEvents.WaitFor<UserMsgs.ClientScopeAdded>(TimeSpan.FromMilliseconds(100));
            _fixture.ClearQueues();


            //when
            var cmd = MessageBuilder.New(
                () => new UserMsgs.RemoveClientScope(_userId, ClientScope));
            _fixture.Dispatcher.Send(cmd);
            //then
            _fixture.RepositoryEvents.WaitFor<UserMsgs.ClientScopeRemoved>(TimeSpan.FromMilliseconds(100));
            _fixture
                .TestQueue
                .AssertNext<UserMsgs.RemoveClientScope>(cmd.CorrelationId)
                .AssertEmpty();
            _fixture
                 .RepositoryEvents
                 .AssertNext<UserMsgs.ClientScopeRemoved>(cmd.CorrelationId, out UserMsgs.ClientScopeRemoved evt)
                 .AssertEmpty();
            Assert.Equal(ClientScope, evt.ClientScope);
        }




        [Fact]
        public void can_deactivate_user()
        {
            AddUser();

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
            //given
            AddUser();
            var evt = _fixture.EventSerializer.Serialize(MessageBuilder.New(
               () => new UserMsgs.Deactivated(
                   _userId)));

            var stream = _fixture.StreamNameBuilder.GenerateForAggregate(typeof(User), _userId);
            _fixture.StreamStoreConnection.AppendToStream(
                stream,
                ExpectedVersion.Any,
                null,
                new[] { evt });
            _fixture.RepositoryEvents.WaitFor<UserMsgs.Deactivated>(TimeSpan.FromMilliseconds(100));
            _fixture.ClearQueues();

            //when
            var cmd = MessageBuilder.New(
                () => new UserMsgs.Activate(_userId));
            _fixture.Dispatcher.Send(cmd);

            _fixture.RepositoryEvents.WaitFor<UserMsgs.Activated>(TimeSpan.FromMilliseconds(100));
            //then
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
        public void can_update_user_details()
        {
            AddUser();
            AddUserDetails();

            var cmd = MessageBuilder.New(
                () => new UserMsgs.UpdateUserDetails(_userId, GiveNameUpdate, SurnameUpdate, FullNameUpdate, EmailUpdate));
            _fixture.Dispatcher.Send(cmd);

            _fixture.RepositoryEvents.WaitFor<UserMsgs.UserDetailsUpdated>(TimeSpan.FromMilliseconds(100));
            _fixture
                .TestQueue
                .AssertNext<UserMsgs.UpdateUserDetails>(cmd.CorrelationId)
                .AssertEmpty();
            _fixture
                .RepositoryEvents
                .AssertNext<UserMsgs.UserDetailsUpdated>(cmd.CorrelationId)
                .AssertEmpty();

        }
        [Fact]
        public void cannot_update_user_details_without_user()
        {
            var cmd = MessageBuilder.New(
                () => new UserMsgs.UpdateUserDetails(_userId, GiveNameUpdate));
            AssertEx.CommandThrows<AggregateNotFoundException>(
                () => _fixture.Dispatcher.Send(cmd));
        }





        private void AddUser()
        {
            var evt = _fixture.EventSerializer.Serialize(MessageBuilder.New(
                () => new UserMsgs.UserCreated(
                    _userId)));
            var evt2 = _fixture.EventSerializer.Serialize(MessageBuilder.New(
                () => new UserMsgs.Activated(
                    _userId)));

            var stream = _fixture.StreamNameBuilder.GenerateForAggregate(typeof(User), _userId);
            _fixture.StreamStoreConnection.AppendToStream(
                stream,
                ExpectedVersion.Any,
                null,
                new[] { evt, evt2 });
            _fixture.RepositoryEvents.WaitFor<UserMsgs.Activated>(TimeSpan.FromMilliseconds(200));
            _fixture.ClearQueues();
        }
        private void AddUserDetails()
        {
            var evt = MessageBuilder.New(
                () => new UserMsgs.UserDetailsUpdated(_userId, GivenName, Surname, FullName, Email));
            var stream = _fixture.StreamNameBuilder.GenerateForAggregate(typeof(User), _userId);
            _fixture.StreamStoreConnection.AppendToStream(
                stream,
                ExpectedVersion.Any,
                null,
                _fixture.EventSerializer.Serialize(evt));
            _fixture.RepositoryEvents.WaitFor<UserMsgs.UserDetailsUpdated>(TimeSpan.FromMilliseconds(100));
            _fixture.ClearQueues();
        }



        public void Dispose()
        {
            _userSvc?.Dispose();
        }
    }

}

