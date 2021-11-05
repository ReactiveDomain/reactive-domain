using ReactiveDomain.Identity.Messages;
using ReactiveDomain.Identity.ReadModels;
using ReactiveDomain.Identity.Domain;
using ReactiveDomain.Messaging;
using ReactiveDomain.Testing;
using System;
using System.Collections.Generic;
using Xunit;

namespace ReactiveDomain.Identity.Tests
{
    public class SubjectRmTests
    {
        private readonly MockRepositorySpecification _fixture;
        private readonly SubjectsRm _rm;
        private Dictionary<string, Dictionary<Guid, Guid>> _subjects = new Dictionary<string, Dictionary<Guid, Guid>>(); //{provider/domain}-{userId-subjectId}

        private string authProvider = "AD";
        private string authProvider2 = "Google";
        private string authDomain = "MyDomain";

        public SubjectRmTests()
        {
            _fixture = new MockRepositorySpecification();
            AddSubjects(5);
            _rm = new SubjectsRm(_fixture.ConfiguredConnection);
        }
        [Fact]
        public void readmodel_lists_existing_subjects()
        {
            Assert.Equal(_subjects, _rm.SubjectsByUserId);
        }
        [Fact]
        public void readmodel_lists_added_subjects()
        {
            AddNewSubject();
            Assert.Equal(_subjects, _rm.SubjectsByUserId);
        }

        [Fact]
        public void readmodel_lists_multiple_domains()
        {
            var userId = Guid.NewGuid();
            AddNewSubject(userId, provider: authProvider, domain: authDomain);
            AddNewSubject(provider: authProvider2, domain: "other1");
            AddNewSubject(provider: authProvider2, domain: "other2");
            Assert.Equal(_subjects, _rm.SubjectsByUserId);
        }
        [Fact]
        public void can_get_subject_ids()
        {
            var user1 = Guid.NewGuid();
            var user2 = Guid.NewGuid();
            var user3 = Guid.NewGuid();
            var sub1 = AddNewSubject(user1, provider: authProvider2, domain: "other1");
            var sub2 = AddNewSubject(user2, provider: authProvider2, domain: "other2");
            var sub3 = AddNewSubject(user3);
            Guid testSub = Guid.Empty;
            AssertEx.IsOrBecomesTrue(()=> _rm.TryGetSubjectIdForUser(user1, authProvider2, "other1", out testSub));
            Assert.Equal(sub1, testSub);
            Assert.True(_rm.TryGetSubjectIdForUser(user2, authProvider2, "other2", out testSub));
            Assert.Equal(sub2, testSub);
            Assert.True(_rm.TryGetSubjectIdForUser(user3, authProvider, authDomain, out testSub));
            Assert.Equal(sub3, testSub);
        }
        [Fact]
        public void can_get_subject_id_for_principle()
        {
            var userId = Guid.NewGuid();            
            var subjectId = AddNewSubject(userId, provider: authProvider, domain: authDomain);
            var user = new MockPrinciple { Provider = authProvider, Domain = authDomain, SId = userId.ToString() };
            Assert.True(_rm.TryGetSubjectIdForPrinciple(user, out var id));
            Assert.Equal(subjectId, id);
        }
        [Fact]
        public void missing_sub_id_returns_false()
        {
            Assert.False(_rm.TryGetSubjectIdForUser(Guid.NewGuid(), authProvider, authDomain, out Guid testSub));
            Assert.Equal(Guid.Empty, testSub);
        }
        [Fact]
        public void wrong_domain_returns_false()
        {
            var user1 = Guid.NewGuid();
            var sub1 = AddNewSubject(user1, authProvider, authDomain);
            //no
            Assert.False(_rm.TryGetSubjectIdForUser(user1, authProvider, "other1", out Guid testSub));
            Assert.Equal(Guid.Empty, testSub);
            //no
            Assert.False(_rm.TryGetSubjectIdForUser(user1, authProvider2, authDomain, out testSub));
            Assert.Equal(Guid.Empty, testSub);
            //yes
            Assert.True(_rm.TryGetSubjectIdForUser(user1, authProvider, authDomain, out testSub));
            Assert.Equal(sub1, testSub);

        }
        private void AddSubjects(int count)
        {
            for (int i = 0; i < count; i++) { AddNewSubject(); }
        }
        private Guid AddNewSubject(Guid? specifiedUserId = null, string provider = null, string domain = null)
        {
            var subjectId = Guid.NewGuid();
            var userId = specifiedUserId ?? Guid.NewGuid();

            if (!_subjects.TryGetValue($"{provider ?? authProvider}-{domain ?? authDomain}", out var subjects))
            {
                subjects = new Dictionary<Guid, Guid>();
                _subjects.Add($"{provider ?? authProvider}-{domain ?? authDomain}", subjects);
            }
            subjects.Add(userId, subjectId);

            var evt = MessageBuilder.New(
              () => new SubjectMsgs.SubjectCreated(subjectId, userId, userId.ToString(), provider ?? authProvider, domain ?? authDomain));
            var stream = _fixture.StreamNameBuilder.GenerateForAggregate(typeof(Subject), subjectId);
            _fixture.StreamStoreConnection.AppendToStream(
                stream,
                ExpectedVersion.Any,
                null,
                new[] { _fixture.EventSerializer.Serialize(evt) });
            _fixture.RepositoryEvents.WaitFor<SubjectMsgs.SubjectCreated>(TimeSpan.FromMilliseconds(200));
            _fixture.ClearQueues();
            return subjectId;
        }
    }
}
