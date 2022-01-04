using System;
using System.Collections.Generic;
using ReactiveDomain.Messaging;
using ReactiveDomain.Users.Domain.Aggregates;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Users.Tests.Helpers;
using Xunit;

namespace ReactiveDomain.Users.Tests
{
    public sealed class ApplicationAggregateTests
    {
        private readonly ICorrelatedMessage _command = MessageBuilder.New(() => new TestMessages.RootCommand());

        private readonly Guid _id = Guid.NewGuid();
        private const string Application = "Kaleido";
        private const bool OneRolePerUser = true;
        private readonly List<string> _roles = new List<string> { "SecAdmin", "Admin", "Editor", "Operator" };
        private const string SecAdminRole = "SecAdmin";
        private const string DefaultUser = "DefaultUserName";
        private const string DefaultDomain = "";
        private readonly List<string> _defaultUserRoles = new List<string>();

        [Fact]
        public void can_create_new_clientapplication()
        {
            var application = new ApplicationRoot(
                            _id,
                            Application,
                            OneRolePerUser,
                            _roles,
                            SecAdminRole,
                            DefaultUser,
                            DefaultDomain,
                            _defaultUserRoles,
                            _command);
            var events = application.TakeEvents();
            Assert.Collection(
                events,
                e =>
                {
                    if (e is ApplicationMsgs.ApplicationRegistered created)

                    {
                        Assert.Equal(_id, created.Id);
                        Assert.Equal(Application, created.Name);
                        Assert.Equal(OneRolePerUser, created.OneRolePerUser);
                        Assert.Equal(_roles, created.Roles);
                        Assert.Equal(SecAdminRole, created.SecAdminRole);
                        Assert.Equal(DefaultUser, created.DefaultUser);
                        Assert.Equal(DefaultDomain, created.DefaultDomain);
                        Assert.Equal(_defaultUserRoles, created.DefaultUserRoles);
                    }
                    else
                    {
                        throw new Exception("wrong event.");
                    }

                });
        }

        [Fact]
        public void cannot_create_application_with_empty_id()
        {
            Assert.Throws<ArgumentException>(
                () => new ApplicationRoot(
                    Guid.Empty, 
                    Application,
                    OneRolePerUser,
                    _roles,
                    SecAdminRole,
                    DefaultUser,
                    DefaultDomain,
                    _defaultUserRoles,
                    _command));
        }

        [Fact]
        public void cannot_create_application_with_empty_name()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ApplicationRoot(
                    _id,
                    string.Empty,
                    OneRolePerUser,
                    _roles,
                    SecAdminRole,
                    DefaultUser,
                    DefaultDomain,
                    _defaultUserRoles,
                    _command));
        }

        [Fact]
        public void cannot_create_application_with_empty_roles()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ApplicationRoot(
                    _id,
                    Application,
                    OneRolePerUser,
                    null, 
                    SecAdminRole,
                    DefaultUser,
                    DefaultDomain,
                    _defaultUserRoles,
                    _command));
        }

        [Fact]
        public void cannot_create_application_with_empty_defaultusername()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ApplicationRoot(
                    _id,
                    Application,
                    OneRolePerUser,
                    _roles,
                    SecAdminRole,
                    string.Empty,
                    DefaultDomain,
                    _defaultUserRoles,
                    _command));
        }
        [Fact]
        public void cannot_create_application_with_empty_secadminrole()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ApplicationRoot(
                    _id,
                    Application,
                    OneRolePerUser,
                    _roles,
                    string.Empty,
                    DefaultUser,
                    DefaultDomain,
                    _defaultUserRoles,
                    _command));
        }
    }
}