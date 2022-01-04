using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Users.Domain.Aggregates;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Users.Tests.Helpers;
using Xunit;

namespace ReactiveDomain.Users.Tests
{
    public sealed class RoleAggregateTests
    {
        private readonly ICorrelatedMessage _command = MessageBuilder.New(() => new TestMessages.RootCommand());

        private readonly Guid _id = Guid.NewGuid();
        private const string Name = "Admin";
        private const string Application = "Kaleido";

        [Fact]
        public void can_create_new_role()
        {
            var role = new Role(
                            _id,
                            Name,
                            Application,
                            _command);
            var events = role.TakeEvents();
            Assert.Collection(
                events,
                e =>
                {
                    if (e is RoleMsgs.RoleCreated created)

                    {
                        Assert.Equal(_id, created.RoleId);
                        Assert.Equal(Name, created.Name);
                        Assert.Equal(Application, created.Application);
                    }
                    else
                    {
                        throw new Exception("wrong event.");
                    }

                });
        }

        [Fact]
        public void cannot_create_role_with_empty_id()
        {
            Assert.Throws<ArgumentException>(
                () => new Role(
                                    Guid.Empty,
                                    Name,
                                    Application,
                                    _command));
        }

        [Fact]
        public void cannot_create_role_with_empty_name()
        {
            Assert.Throws<ArgumentNullException>(
                () => new Role(
                                    _id,
                                    string.Empty,
                                    Application,
                                    _command));
        }

        [Fact]
        public void cannot_create_role_with_empty_application()
        {
            Assert.Throws<ArgumentNullException>(
                () => new Role(
                                    _id,
                                    Name,
                                    string.Empty,
                                    _command));
        }

        [Fact]
        public void can_remove_role()
        {
            var role = new Role(
                            _id,
                            Name,
                            Application,
                            _command);

            role.Remove();

            var events = role.TakeEvents();

            Assert.Collection(
                events,
                e => Assert.IsType<RoleMsgs.RoleCreated>(e),
                e =>
                {
                    if (e is RoleMsgs.RoleRemoved removed)
                    {
                        Assert.Equal(_id, removed.RoleId);
                    }
                    else
                    {
                        throw new Exception("wrong event.");
                    }
                });
        }

    }
}