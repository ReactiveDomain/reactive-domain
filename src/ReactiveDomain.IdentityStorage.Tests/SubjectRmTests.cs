﻿using ReactiveDomain.Messaging;
using ReactiveDomain.Testing;
using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveDomain.IdentityStorage.Domain;
using ReactiveDomain.IdentityStorage.Messages;
using ReactiveDomain.IdentityStorage.ReadModels;
using Xunit;

namespace ReactiveDomain.IdentityStorage.Tests
{
    public class SubjectRmTests
    {
        private readonly SubjectsRm _rm;
        private readonly Dictionary<string, Dictionary<Guid, Guid>> _subjects = new Dictionary<string, Dictionary<Guid, Guid>>(); //{provider/domain}-{userId-subjectId}

        private const string AuthProvider = "AD";
        private const string AuthProvider2 = "Google";
        private const string AuthDomain = "MyDomain";

        public SubjectRmTests()
        {           
            _rm = new SubjectsRm(new NullConfiguredConnection());
            AddSubjects(5);
        }
        [Fact]
        public void readmodel_lists_existing_subjects()
        {
            AssertEx.IsOrBecomesTrue(() => _rm.SubjectsByUserId.Any() && _subjects.First().Value.Count == _rm.SubjectsByUserId.First().Value.Count);
            Assert.Equal(_subjects, _rm.SubjectsByUserId);
        }
        [Fact]
        public void readmodel_lists_added_subjects()
        {
            AddNewSubject();
            AssertEx.IsOrBecomesTrue(() => _rm.SubjectsByUserId.Any() && _subjects.First().Value.Count == _rm.SubjectsByUserId.First().Value.Count);
            Assert.Equal(_subjects, _rm.SubjectsByUserId);
        }

        [Fact]
        public void readmodel_lists_multiple_domains()
        {
            var userId = Guid.NewGuid();
            AddNewSubject(userId, provider: AuthProvider, domain: AuthDomain);
            AddNewSubject(provider: AuthProvider2, domain: "other1");
            AddNewSubject(provider: AuthProvider2, domain: "other2");
            AssertEx.IsOrBecomesTrue(() => _rm.SubjectsByUserId.Any() && _subjects.First().Value.Count == _rm.SubjectsByUserId.First().Value.Count);
            AssertEx.IsOrBecomesTrue(() => _rm.SubjectsByUserId.Any() && _subjects.Last().Value.Count == _rm.SubjectsByUserId.Last().Value.Count);
            Assert.Equal(_subjects, _rm.SubjectsByUserId);
        }
        [Fact]
        public void can_get_subject_ids()
        {
            var user1 = Guid.NewGuid();
            var user2 = Guid.NewGuid();
            var user3 = Guid.NewGuid();
            var sub1 = AddNewSubject(user1, provider: AuthProvider2, domain: "other1");
            var sub2 = AddNewSubject(user2, provider: AuthProvider2, domain: "other2");
            var sub3 = AddNewSubject(user3);
            AssertEx.IsOrBecomesTrue(() => _subjects.Count == _rm.SubjectsByUserId.Count);
            AssertEx.IsOrBecomesTrue(() => _subjects.First().Value.Count == _rm.SubjectsByUserId.First().Value.Count);
            AssertEx.IsOrBecomesTrue(() => _subjects.Last().Value.Count == _rm.SubjectsByUserId.Last().Value.Count);
            var testSub = Guid.Empty;
            AssertEx.IsOrBecomesTrue(()=> _rm.TryGetSubjectIdForUser(user1, AuthProvider2, "other1", out testSub));
            Assert.Equal(sub1, testSub);
            Assert.True(_rm.TryGetSubjectIdForUser(user2, AuthProvider2, "other2", out testSub));
            Assert.Equal(sub2, testSub);
            Assert.True(_rm.TryGetSubjectIdForUser(user3, AuthProvider, AuthDomain, out testSub));
            Assert.Equal(sub3, testSub);
        }
        [Fact]
        public void can_get_subject_id_for_principal()
        {
            var userId = Guid.NewGuid();            
            var subjectId = AddNewSubject(userId, provider: AuthProvider, domain: AuthDomain);
            var user = new MockPrincipal { Provider = AuthProvider, Domain = AuthDomain, SId = userId.ToString() };
            var id = Guid.Empty;
            AssertEx.IsOrBecomesTrue(() => _rm.TryGetSubjectIdForPrincipal(user, out id));
            Assert.Equal(subjectId, id);
        }
        [Fact]
        public void missing_sub_id_returns_false()
        {
            Assert.False(_rm.TryGetSubjectIdForUser(Guid.NewGuid(), AuthProvider, AuthDomain, out Guid testSub));
            Assert.Equal(Guid.Empty, testSub);
        }
        [Fact]
        public void wrong_domain_returns_false()
        {
            var user1 = Guid.NewGuid();
            var sub1 = AddNewSubject(user1, AuthProvider, AuthDomain);
            //yes
            var testSub = Guid.Empty;
            AssertEx.IsOrBecomesTrue(() => _rm.TryGetSubjectIdForUser(user1, AuthProvider, AuthDomain, out testSub));
            Assert.Equal(sub1, testSub);
            //no
            Assert.False(_rm.TryGetSubjectIdForUser(user1, AuthProvider, "other1", out testSub));
            Assert.Equal(Guid.Empty, testSub);
            //no
            Assert.False(_rm.TryGetSubjectIdForUser(user1, AuthProvider2, AuthDomain, out testSub));
            Assert.Equal(Guid.Empty, testSub);
        }
        private void AddSubjects(int count)
        {
            for (int i = 0; i < count; i++) { AddNewSubject(); }
        }
        private Guid AddNewSubject(Guid? specifiedUserId = null, string provider = null, string domain = null)
        {
            var subjectId = Guid.NewGuid();
            var userId = specifiedUserId ?? Guid.NewGuid();

            if (!_subjects.TryGetValue($"{provider ?? AuthProvider}-{(domain ?? AuthDomain).ToLowerInvariant()}", out var subjects))
            {
                subjects = new Dictionary<Guid, Guid>();
                _subjects.Add($"{provider ?? AuthProvider}-{(domain ?? AuthDomain).ToLowerInvariant()}", subjects);
            }
            subjects.Add(userId, subjectId);

            var evt = MessageBuilder.New(
              () => new SubjectMsgs.SubjectCreated(subjectId, userId, userId.ToString(), provider ?? AuthProvider, (domain ?? AuthDomain).ToLowerInvariant()));
            _rm.DirectApply(evt);                 
            return subjectId;
        }
    }
}
