using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Testing;
using ReactiveDomain.Users.Domain;
using ReactiveDomain.Users.Domain.Aggregates;
using ReactiveDomain.Users.Domain.Services;
using ReactiveDomain.Users.Messages;
using Xunit;

namespace ReactiveDomain.Users.Tests
{
    [Collection("RoleDomainTests")]
    public sealed class RoleServiceTests :
        IDisposable
    {
        private readonly MockRepositorySpecification _fixture;
        private readonly RoleSvc _roleSvc;

        private readonly Guid _roleId = Guid.NewGuid();
        private const string RoleName = "Admin";
        private const string Application = "Kaleido";


        public RoleServiceTests()
        {
            _fixture = new MockRepositorySpecification();
           
            _roleSvc = new RoleSvc(()=>_fixture.GetListener(nameof(RoleServiceTests)), _fixture.Repository, _fixture.Dispatcher);
        }

        public void Dispose()
        {
            _roleSvc.Dispose();
        }

        [Fact]
        public void can_create_new_role()
        {
            var cmd = MessageBuilder.New(
                        () => new RoleMsgs.CreateRole(
                                    _roleId, 
                                    RoleName, 
                                    Application));
            _fixture.Dispatcher.Send(cmd);
            _fixture.RepositoryEvents.WaitFor<RoleMsgs.RoleCreated>(TimeSpan.FromMilliseconds(100));
            _fixture
                .TestQueue
                .AssertNext<RoleMsgs.CreateRole>(cmd.CorrelationId)
                .AssertEmpty();
            _fixture
                .RepositoryEvents
                .AssertNext<RoleMsgs.RoleCreated>(cmd.CorrelationId)
                .AssertEmpty();
        }

        [Fact]
        public void cannot_create_duplicate_role()
        {
            AddRole();
            AssertEx.CommandThrows<DuplicateRoleException>(
                () => _fixture.Dispatcher.Send(
                        MessageBuilder.New(() => new RoleMsgs.CreateRole(
                                                        _roleId,
                                                        RoleName,
                                                        Application))));
        }

        private void AddRole()
        {
            var evt = MessageBuilder.New(
                        () => new RoleMsgs.RoleCreated(
                                    _roleId,
                                    RoleName,
                                    Application));
            var stream = _fixture.StreamNameBuilder.GenerateForAggregate(typeof(Role), _roleId);
            _fixture.StreamStoreConnection.AppendToStream(
                stream,
                ExpectedVersion.Any,
                null,
                _fixture.EventSerializer.Serialize(evt));
            _fixture.RepositoryEvents.WaitFor<RoleMsgs.RoleCreated>(TimeSpan.FromMilliseconds(100));
            _fixture.ClearQueues();
        }

    }
}
