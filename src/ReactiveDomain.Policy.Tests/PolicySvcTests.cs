using ReactiveDomain.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactiveDomain.Policy.Tests
{
    public class PolicySvcTests
    {
        /*
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
        */
    }
}
