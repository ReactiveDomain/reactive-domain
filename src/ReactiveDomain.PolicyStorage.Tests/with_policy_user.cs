using ReactiveDomain.Messaging;
using ReactiveDomain.Policy.Domain;
using ReactiveDomain.Policy.Messages;
using ReactiveDomain.Users.Tests.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ReactiveDomain.PolicyStorage.Tests
{
    public class with_policy_user
    {
        Guid _policyId = Guid.NewGuid();
        Guid _userId = Guid.NewGuid();
        Guid _id = Guid.NewGuid();
        private readonly ICorrelatedMessage _command = MessageBuilder.New(() => new TestMessages.RootCommand());

        public with_policy_user()
        {

        }
        [Fact]
        public void can_create_policy_user()
        {

            var user = new PolicyUser(
                               _id,
                               _policyId,
                               _userId,
                               false,
                               _command)
            { };

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
                              _command)
            { };

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
                              _command)
            { };


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
            Guid role1_Id = Guid.NewGuid();
            string role1_name = "role1";
            Guid role2_Id = Guid.NewGuid();
            string role2_name = "role2";

            var user = new PolicyUser(
                           _id,
                           _policyId,
                           _userId,
                           false,
                           _command)
            { };
            //add role           
            user.AddRole(role1_name, role1_Id);
            user.AddRole(role2_name, role2_Id);
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
                                  Assert.Equal(role1_Id, removed.RoleId);
                                  Assert.Equal(role1_name, removed.RoleName);
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
                                  Assert.Equal(role2_Id, removed.RoleId);
                                  Assert.Equal(role2_name, removed.RoleName);
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
            Guid role1_Id = Guid.NewGuid();
            string role1_name = "role1";
            Guid role2_Id = Guid.NewGuid();
            string role2_name = "role2";

            var user = new PolicyUser(
                           _id,
                           _policyId,
                           _userId,
                           false,
                           _command)
            { };
            //add roles           
            user.AddRole(role1_name, role1_Id);
            user.AddRole(role2_name, role2_Id);
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
                                  Assert.Equal(role1_Id, added.RoleId);
                                  Assert.Equal(role1_name, added.RoleName);
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
                                  Assert.Equal(role2_Id, added.RoleId);
                                  Assert.Equal(role2_name, added.RoleName);
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
    }
}
