using ReactiveDomain.Identity.Domain;
using ReactiveDomain.Identity.Messages;
using ReactiveDomain.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ReactiveDomain.Identity.Tests
{
    public class SubjectAggTests
    {
        private readonly ICorrelatedMessage _command = MessageBuilder.New(() => new TestMessages.RootCommand());

        private readonly Guid _subjectId = Guid.NewGuid();
        private readonly Guid _userId = Guid.NewGuid();

        private string subClaim = "subject1";
        private string authProvider = "AD";
        private string authDomain = "LocalHost";
        private string hostIpAddress = "127.0.0.1";

        [Fact]
        public void can_create_subject()
        {
            Assert.Throws<ArgumentException>(() => new Subject(Guid.Empty, _userId, subClaim, authProvider, authDomain, _command));
            Assert.Throws<ArgumentException>(() => new Subject(_subjectId, Guid.Empty, subClaim, authProvider, authDomain, _command));
            Assert.Throws<ArgumentNullException>(() => new Subject(_subjectId, _userId, null, authProvider, authDomain, _command));
            Assert.Throws<ArgumentNullException>(() => new Subject(_subjectId, _userId, String.Empty, authProvider, authDomain, _command));
            Assert.Throws<ArgumentNullException>(() => new Subject(_subjectId, _userId, subClaim, null, authDomain, _command));
            Assert.Throws<ArgumentNullException>(() => new Subject(_subjectId, _userId, subClaim, String.Empty, authDomain, _command));
            Assert.Throws<ArgumentNullException>(() => new Subject(_subjectId, _userId, subClaim, authProvider, null, _command));
            Assert.Throws<ArgumentNullException>(() => new Subject(_subjectId, _userId, subClaim, authProvider, String.Empty, _command));
            Assert.Throws<ArgumentNullException>(() => new Subject(_subjectId, _userId, subClaim, authProvider, authDomain, null));

            var sub = new Subject(_subjectId, _userId, subClaim, authProvider, authDomain, _command);
            var events = ((IEventSource)sub).TakeEvents();
            Assert.Collection(
                           events,
                           e =>
                           {
                               var created = Assert.IsType<SubjectMsgs.SubjectCreated>(e);
                               Assert.Equal(_subjectId, created.SubjectId);
                               Assert.Equal(_userId, created.UserId);
                               Assert.Equal(subClaim, created.SubClaim);
                               Assert.Equal(authProvider, created.AuthProvider);
                               Assert.Equal(authDomain, created.AuthDomain);
                               Assert.Equal(_command.CorrelationId, created.CorrelationId);
                               Assert.Equal(_command.MsgId, created.CausationId);
                           });


        }
        [Fact]
        public void can_log_authenticated_subject()
        {

            var sub = new Subject(_subjectId, _userId, subClaim, authProvider, authDomain, _command);
            sub.Authenticated(hostIpAddress);

            var events = ((IEventSource)sub).TakeEvents();
            Assert.Collection(
                           events,
                            e => Assert.IsType<SubjectMsgs.SubjectCreated>(e),
                            e =>
                            {
                                var authenticated = Assert.IsType<SubjectMsgs.Authenticated>(e);
                                Assert.Equal(_subjectId, authenticated.SubjectId);
                                Assert.Equal(hostIpAddress, authenticated.HostIpAddress);
                            });
        }

        [Fact]
        public void can_log_authenticated_failed_account_locked()
        {

            var sub = new Subject(_subjectId, _userId, subClaim, authProvider, authDomain, _command);
            sub.NotAuthenticatedAccountLocked(hostIpAddress);

            var events = ((IEventSource)sub).TakeEvents();
            Assert.Collection(
                           events,
                            e => Assert.IsType<SubjectMsgs.SubjectCreated>(e),
                            e =>
                            {
                                var authenticated = Assert.IsType<SubjectMsgs.AuthenticationFailedAccountLocked>(e);
                                Assert.Equal(_subjectId, authenticated.SubjectId);
                                Assert.Equal(hostIpAddress, authenticated.HostIpAddress);
                            });
        }
        [Fact]
        public void can_log_authenticated_failed_account_disabled()
        {

            var sub = new Subject(_subjectId, _userId, subClaim, authProvider, authDomain, _command);
            sub.NotAuthenticatedAccountDisabled(hostIpAddress);

            var events = ((IEventSource)sub).TakeEvents();
            Assert.Collection(
                           events,
                            e => Assert.IsType<SubjectMsgs.SubjectCreated>(e),
                            e =>
                            {
                                var authenticated = Assert.IsType<SubjectMsgs.AuthenticationFailedAccountDisabled>(e);
                                Assert.Equal(_subjectId, authenticated.SubjectId);
                                Assert.Equal(hostIpAddress, authenticated.HostIpAddress);
                            });
        }
        [Fact]
        public void can_log_authenticated_failed_invalid_credentials()
        {

            var sub = new Subject(_subjectId, _userId, subClaim, authProvider, authDomain, _command);
            sub.NotAuthenticatedInvalidCredentials(hostIpAddress);

            var events = ((IEventSource)sub).TakeEvents();
            Assert.Collection(
                           events,
                            e => Assert.IsType<SubjectMsgs.SubjectCreated>(e),
                            e =>
                            {
                                var authenticated = Assert.IsType<SubjectMsgs.AuthenticationFailedInvalidCredentials>(e);
                                Assert.Equal(_subjectId, authenticated.SubjectId);
                                Assert.Equal(hostIpAddress, authenticated.HostIpAddress);
                            });
        }       
    }
}
