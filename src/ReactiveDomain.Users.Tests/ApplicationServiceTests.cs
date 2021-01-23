using System;
using System.Collections.Generic;
using ReactiveDomain.Messaging;
using ReactiveDomain.Testing;
using ReactiveDomain.Users.Domain;
using ReactiveDomain.Users.Domain.Aggregates;
using ReactiveDomain.Users.Domain.Services;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Users.Tests.Helpers;
using Xunit;

namespace ReactiveDomain.Users.Tests
{
    [Collection("ApplicationDomainTests")]
    public sealed class ApplicationServiceTests :
        IDisposable
    {
        private readonly ICorrelatedMessage _command = MessageBuilder.New(() => new TestMessages.RootCommand());

        private readonly MockRepositorySpecification _fixture;
        private readonly ApplicationSvc _applicationSvc;

        private readonly Guid _id = Guid.NewGuid();
        private const string Application = "Kaleido";
        private const bool OneRolePerUser = true;
        private readonly List<string> _roles = new List<string> { "SecAdmin", "Admin", "Editor", "Operator" };
        private const string SecAdminRole = "SecAdmin";
        private const string DefaultUser = "DefaultUserName";
        private const string DefaultDomain = "";
        private readonly List<string> _defaultUserRoles = new List<string>();
        


        public ApplicationServiceTests()
        {
            _fixture = new MockRepositorySpecification();
            _applicationSvc = new ApplicationSvc( _fixture.Repository, ()=> _fixture.GetListener(nameof(ApplicationServiceTests)), _fixture.Dispatcher);
        }

        public void Dispose()
        {
            _applicationSvc.Dispose();
        }

        [Fact]
        public void can_create_new_application()
        {
            var cmd = MessageBuilder.New(
                        () => new ApplicationMsgs.RegisterApplication(
                            _id, 
                            Application, 
                            OneRolePerUser, 
                            _roles, 
                            SecAdminRole, 
                            DefaultUser, 
                            DefaultDomain,
                            _defaultUserRoles));
            _fixture.Dispatcher.Send(cmd);

            _fixture.RepositoryEvents.WaitFor<ApplicationMsgs.ApplicationRegistered>(TimeSpan.FromMilliseconds(100));
            _fixture
                .TestQueue
                .AssertNext<ApplicationMsgs.RegisterApplication>(cmd.CorrelationId)
                .AssertEmpty();
            _fixture
                .RepositoryEvents
                .AssertNext<ApplicationMsgs.ApplicationRegistered>(cmd.CorrelationId)
                .AssertEmpty();
        }

        [Fact]
        public void cannot_create_duplicate_application()
        {
            AddApplication();
            var cmd = MessageBuilder.New(
                () => new ApplicationMsgs.RegisterApplication(
                    _id,
                    Application,
                    OneRolePerUser,
                    _roles,
                    SecAdminRole,
                    DefaultUser,
                    DefaultDomain,
                    _defaultUserRoles));
            AssertEx.CommandThrows<DuplicateApplicationException>(() => _fixture.Dispatcher.Send(cmd));
            _fixture
                .TestQueue
                .AssertNext<ApplicationMsgs.RegisterApplication>(cmd.CorrelationId)
                .AssertEmpty();
            _fixture.RepositoryEvents.AssertEmpty();
        }

        private void AddApplication()
        {
            var evt = MessageBuilder.New(
                () => new ApplicationMsgs.ApplicationRegistered(_id,
                    Application,
                    OneRolePerUser,
                    _roles,
                    SecAdminRole,
                    DefaultUser,
                    DefaultDomain,
                    _defaultUserRoles));
            var stream = _fixture.StreamNameBuilder.GenerateForAggregate(typeof(ApplicationRoot), _id);
            _fixture.StreamStoreConnection.AppendToStream(
                stream,
                ExpectedVersion.Any,
                null,
                _fixture.EventSerializer.Serialize(evt));
            _fixture.RepositoryEvents.WaitFor<ApplicationMsgs.ApplicationRegistered>(TimeSpan.FromMilliseconds(100));
            _fixture.ClearQueues();
            
        }
    }

}

