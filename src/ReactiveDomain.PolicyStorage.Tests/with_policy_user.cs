using ReactiveDomain.Messaging;
using ReactiveDomain.Policy.Domain;
using ReactiveDomain.Policy.Messages;
using ReactiveDomain.Policy.Tests.Helpers;
using System;
using Xunit;

namespace ReactiveDomain.PolicyStorage.Tests
{
    public class with_policy_user
    {
        private readonly Guid _policyId = Guid.NewGuid();
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _id = Guid.NewGuid();
        private readonly ICorrelatedMessage _command = MessageBuilder.New(() => new TestMessages.RootCommand());

        [Fact]
        public void can_create_policy_user()
        {

            var user = new PolicyUser(
                               _id,
                               _policyId,
                               _userId,
                               false,
                               _command);

            var events = user.TakeEvents();
            Assert.Collection(
                            events,
                            e =>
                            {
                                if (e is PolicyUserMsgs.PolicyUserAdded added)
                                {
                                    Assert.Equal(_id, added.PolicyUserId);
                                    Assert.Equal(_policyId, added.PolicyId);
                                    Assert.Equal(_userId, added.UserId);
                                }
                                else
                                {
                                    throw new Exception("wrong event.");
                                }
                            });
        }
        [Fact]
        public void can_add_role()
        {
            Guid roleId = Guid.NewGuid();
            string roleName = "test role";
            var user = new PolicyUser(
                              _id,
                              _policyId,
                              _userId,
                              false,
                              _command);

            user.TakeEvents();
            //add role
            ((ICorrelatedEventSource)user).Source = _command;
            user.AddRole(roleName, roleId);

            var events = user.TakeEvents();
            Assert.Collection(
                            events,
                            e =>
                            {
                                if (e is PolicyUserMsgs.RoleAdded added)
                                {
                                    Assert.Equal(_id, added.PolicyUserId);
                                    Assert.Equal(roleId, added.RoleId);
                                    Assert.Equal(roleName, added.RoleName);
                                }
                                else
                                {
                                    throw new Exception("wrong event.");
                                }
                            });

            //idempotent add
            ((ICorrelatedEventSource)user).Source = _command;
            user.AddRole(roleName, roleId);

            events = user.TakeEvents();


            Assert.Empty(events);
        }

        [Fact]
        public void can_remove_role()
        {
            Guid roleId = Guid.NewGuid();
            string roleName = "test role";
            var user = new PolicyUser(
                              _id,
                              _policyId,
                              _userId,
                              false,
                              _command);


            //add role           
            user.AddRole(roleName, roleId);
            user.TakeEvents();

            //remove role
            ((ICorrelatedEventSource)user).Source = _command;
            user.RemoveRole(roleName, roleId);
            var events = user.TakeEvents();
            Assert.Collection(
                          events,
                          e =>
                          {
                              if (e is PolicyUserMsgs.RoleRemoved added)
                              {
                                  Assert.Equal(_id, added.PolicyUserId);
                                  Assert.Equal(roleId, added.RoleId);
                                  Assert.Equal(roleName, added.RoleName);
                              }
                              else
                              {
                                  throw new Exception("wrong event.");
                              }
                          });

            //Idempotent duplicate remove
            ((ICorrelatedEventSource)user).Source = _command;
            user.RemoveRole(roleName, roleId);
            events = user.TakeEvents();
            Assert.Empty(events);

            //Idempotent non-assigned role remove
            ((ICorrelatedEventSource)user).Source = _command;
            user.RemoveRole("other role", roleId);
            events = user.TakeEvents();
            Assert.Empty(events);
        }
        [Fact]
        public void can_deactivate()
        {
            Guid role1Id = Guid.NewGuid();
            string role1Name = "role1";
            Guid role2Id = Guid.NewGuid();
            string role2Name = "role2";

            var user = new PolicyUser(
                           _id,
                           _policyId,
                           _userId,
                           false,
                           _command)
            { };
            //add role           
            user.AddRole(role1Name, role1Id);
            user.AddRole(role2Name, role2Id);
            user.TakeEvents();

            //deactivate
            ((ICorrelatedEventSource)user).Source = _command;
            user.Deactivate();
            var events = user.TakeEvents();
            Assert.Collection(
                          events,
                          e =>
                          {
                              if (e is PolicyUserMsgs.UserDeactivated deactivated)
                              {
                                  Assert.Equal(_id, deactivated.PolicyUserId);
                              }
                              else
                              {
                                  throw new Exception("wrong event.");
                              }
                          },
                            e =>
                          {
                              if (e is PolicyUserMsgs.RoleRemoved removed)
                              {
                                  Assert.Equal(_id, removed.PolicyUserId);
                                  Assert.Equal(role1Id, removed.RoleId);
                                  Assert.Equal(role1Name, removed.RoleName);
                              }
                              else
                              {
                                  throw new Exception("wrong event.");
                              }
                          },
                              e =>
                          {
                              if (e is PolicyUserMsgs.RoleRemoved removed)
                              {
                                  Assert.Equal(_id, removed.PolicyUserId);
                                  Assert.Equal(role2Id, removed.RoleId);
                                  Assert.Equal(role2Name, removed.RoleName);
                              }
                              else
                              {
                                  throw new Exception("wrong event.");
                              }
                          });
            //Idempotent deactivate
            ((ICorrelatedEventSource)user).Source = _command;
            user.Deactivate();
            events = user.TakeEvents();
            Assert.Empty(events);

        }
        [Fact]
        public void can_reactivate()
        {
            Guid role1Id = Guid.NewGuid();
            string role1Name = "role1";
            Guid role2Id = Guid.NewGuid();
            string role2Name = "role2";

            var user = new PolicyUser(
                           _id,
                           _policyId,
                           _userId,
                           false,
                           _command);
            //add roles           
            user.AddRole(role1Name, role1Id);
            user.AddRole(role2Name, role2Id);
            //deactivate
            user.Deactivate();
            user.TakeEvents();


            ((ICorrelatedEventSource)user).Source = _command;
            user.Reactivate();
            var events = user.TakeEvents();
            Assert.Collection(
                          events,
                          e =>
                          {
                              if (e is PolicyUserMsgs.UserReactivated reactivated)
                              {
                                  Assert.Equal(_id, reactivated.PolicyUserId);
                              }
                              else
                              {
                                  throw new Exception("wrong event.");
                              }
                          },
                            e =>
                          {
                              if (e is PolicyUserMsgs.RoleAdded added)
                              {
                                  Assert.Equal(_id, added.PolicyUserId);
                                  Assert.Equal(role1Id, added.RoleId);
                                  Assert.Equal(role1Name, added.RoleName);
                              }
                              else
                              {
                                  throw new Exception("wrong event.");
                              }
                          },
                              e =>
                          {
                              if (e is PolicyUserMsgs.RoleAdded added)
                              {
                                  Assert.Equal(_id, added.PolicyUserId);
                                  Assert.Equal(role2Id, added.RoleId);
                                  Assert.Equal(role2Name, added.RoleName);
                              }
                              else
                              {
                                  throw new Exception("wrong event.");
                              }
                          });
            //Idempotent reactivate
            ((ICorrelatedEventSource)user).Source = _command;
            user.Reactivate();
            events = user.TakeEvents();
            Assert.Empty(events);
        }

        [Fact]
        public void role_names_are_case_insensitive()
        {
            var roleId = Guid.NewGuid();
            const string roleName = "admin";
            const string roleName2 = "Admin";

            var user = new PolicyUser(
                            _id,
                            _policyId,
                            _userId,
                            false,
                            _command);
            user.AddRole(roleName, roleId);
            user.AddRole(roleName2, roleId); // case-insensitive, idempotent
            user.RemoveRole(roleName2, roleId); // remove using same ID, different case
            var events = user.TakeEvents();
            Assert.Collection(
                events,
                e => Assert.IsType<PolicyUserMsgs.PolicyUserAdded>(e),
                e => Assert.IsType<PolicyUserMsgs.RoleAdded>(e),
                e => Assert.IsType<PolicyUserMsgs.RoleRemoved>(e));
        }

        [Fact]
        public void cannot_add_same_named_role_with_different_id()
        {
            var roleId = Guid.NewGuid();
            const string roleName = "admin";

            var user = new PolicyUser(
                _id,
                _policyId,
                _userId,
                false,
                _command);
            user.AddRole(roleName, roleId);
            Assert.Throws<ArgumentException>(() => user.AddRole(roleName, Guid.NewGuid()));
            var events = user.TakeEvents();
            Assert.Collection(
                events,
                e => Assert.IsType<PolicyUserMsgs.PolicyUserAdded>(e),
                e => Assert.IsType<PolicyUserMsgs.RoleAdded>(e));
        }
    }
}
