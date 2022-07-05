using ReactiveDomain.Messaging;
using System;
using ReactiveDomain.IdentityStorage.Domain;
using ReactiveDomain.IdentityStorage.Messages;
using Xunit;

namespace ReactiveDomain.IdentityStorage.Tests
{
    public class SubjectAggTests
    {
        private readonly ICorrelatedMessage _command = MessageBuilder.New(() => new TestMessages.RootCommand());

        private readonly Guid _subjectId = Guid.NewGuid();
        private readonly Guid _userId = Guid.NewGuid();

        private const string SubClaim = "subject1";
        private const string AuthProvider = "AD";
        private const string AuthDomain = "LocalHost";
        private const string HostIpAddress = "127.0.0.1";
        private const string ClientId = "Application1";

        [Fact]
        public void can_create_subject()
        {
            Assert.Throws<ArgumentException>(() => new Subject(Guid.Empty, _userId, SubClaim, AuthProvider, AuthDomain, _command));
            Assert.Throws<ArgumentException>(() => new Subject(_subjectId, Guid.Empty, SubClaim, AuthProvider, AuthDomain, _command));
            Assert.Throws<ArgumentNullException>(() => new Subject(_subjectId, _userId, null, AuthProvider, AuthDomain, _command));
            Assert.Throws<ArgumentNullException>(() => new Subject(_subjectId, _userId, string.Empty, AuthProvider, AuthDomain, _command));
            Assert.Throws<ArgumentNullException>(() => new Subject(_subjectId, _userId, SubClaim, null, AuthDomain, _command));
            Assert.Throws<ArgumentNullException>(() => new Subject(_subjectId, _userId, SubClaim, string.Empty, AuthDomain, _command));
            Assert.Throws<ArgumentNullException>(() => new Subject(_subjectId, _userId, SubClaim, AuthProvider, null, _command));
            Assert.Throws<ArgumentNullException>(() => new Subject(_subjectId, _userId, SubClaim, AuthProvider, string.Empty, _command));
            Assert.Throws<ArgumentNullException>(() => new Subject(_subjectId, _userId, SubClaim, AuthProvider, AuthDomain, null));

            var sub = new Subject(_subjectId, _userId, SubClaim, AuthProvider, AuthDomain, _command);
            var events = ((IEventSource)sub).TakeEvents();
            Assert.Collection(
                           events,
                           e =>
                           {
                               var created = Assert.IsType<SubjectMsgs.SubjectCreated>(e);
                               Assert.Equal(_subjectId, created.SubjectId);
                               Assert.Equal(_userId, created.UserId);
                               Assert.Equal(SubClaim, created.SubClaim);
                               Assert.Equal(AuthProvider, created.AuthProvider);
                               Assert.Equal(AuthDomain.ToLowerInvariant(), created.AuthDomain);
                               Assert.Equal(_command.CorrelationId, created.CorrelationId);
                               Assert.Equal(_command.MsgId, created.CausationId);
                           });


        }
        [Fact]
        public void can_log_authenticated_subject()
        {

            var sub = new Subject(_subjectId, _userId, SubClaim, AuthProvider, AuthDomain, _command);
            sub.Authenticated(HostIpAddress, ClientId);

            var events = ((IEventSource)sub).TakeEvents();
            Assert.Collection(
                           events,
                            e => Assert.IsType<SubjectMsgs.SubjectCreated>(e),
                            e =>
                            {
                                var authenticated = Assert.IsType<SubjectMsgs.Authenticated>(e);
                                Assert.Equal(_subjectId, authenticated.SubjectId);
                                Assert.Equal(HostIpAddress, authenticated.HostIpAddress);
                                Assert.Equal(ClientId, authenticated.ClientId);
                            });
        }

        [Fact]
        public void can_log_authenticated_failed_account_locked()
        {

            var sub = new Subject(_subjectId, _userId, SubClaim, AuthProvider, AuthDomain, _command);
            sub.NotAuthenticatedAccountLocked(HostIpAddress, ClientId);

            var events = ((IEventSource)sub).TakeEvents();
            Assert.Collection(
                           events,
                            e => Assert.IsType<SubjectMsgs.SubjectCreated>(e),
                            e =>
                            {
                                var authenticated = Assert.IsType<SubjectMsgs.AuthenticationFailedAccountLocked>(e);
                                Assert.Equal(_subjectId, authenticated.SubjectId);
                                Assert.Equal(HostIpAddress, authenticated.HostIpAddress);
                                Assert.Equal(ClientId, authenticated.ClientId);
                            });
        }
        [Fact]
        public void can_log_authenticated_failed_account_disabled()
        {

            var sub = new Subject(_subjectId, _userId, SubClaim, AuthProvider, AuthDomain, _command);
            sub.NotAuthenticatedAccountDisabled(HostIpAddress, ClientId);

            var events = ((IEventSource)sub).TakeEvents();
            Assert.Collection(
                           events,
                            e => Assert.IsType<SubjectMsgs.SubjectCreated>(e),
                            e =>
                            {
                                var authenticated = Assert.IsType<SubjectMsgs.AuthenticationFailedAccountDisabled>(e);
                                Assert.Equal(_subjectId, authenticated.SubjectId);
                                Assert.Equal(HostIpAddress, authenticated.HostIpAddress);
                                Assert.Equal(ClientId, authenticated.ClientId);
                            });
        }
        [Fact]
        public void can_log_authenticated_failed_invalid_credentials()
        {

            var sub = new Subject(_subjectId, _userId, SubClaim, AuthProvider, AuthDomain, _command);
            sub.NotAuthenticatedInvalidCredentials(HostIpAddress, ClientId);

            var events = ((IEventSource)sub).TakeEvents();
            Assert.Collection(
                           events,
                            e => Assert.IsType<SubjectMsgs.SubjectCreated>(e),
                            e =>
                            {
                                var authenticated = Assert.IsType<SubjectMsgs.AuthenticationFailedInvalidCredentials>(e);
                                Assert.Equal(_subjectId, authenticated.SubjectId);
                                Assert.Equal(HostIpAddress, authenticated.HostIpAddress);
                                Assert.Equal(ClientId, authenticated.ClientId);
                            });
        }
    }
}
