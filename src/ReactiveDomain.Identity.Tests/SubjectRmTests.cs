using ReactiveDomain.Identity.Messages;
using ReactiveDomain.Identity.ReadModels;
using ReactiveDomain.Identity.Domain;
using ReactiveDomain.Messaging;
using ReactiveDomain.Testing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ReactiveDomain.Identity.Tests
{
    public class SubjectRmTests
    {
        private readonly MockRepositorySpecification _fixture;
        private readonly SubjectsRm _rm;
        private Dictionary<Guid, Guid> _subjectByUser = new Dictionary<Guid, Guid>(); //userId-subjectId
       
        private string authProvider = "AD";
        private string authDomain = "LocalHost";
        public SubjectRmTests()
        {
            _fixture = new MockRepositorySpecification();
            AddSubjects(5);
            _rm = new SubjectsRm(_fixture.ConfiguredConnection);
        }
        [Fact]
        public void readmodel_lists_existing_subjects()
        {
            Assert.Equal(_subjectByUser, _rm.SubjectIdByUserId);
        }
        [Fact]
        public void readmodel_lists_added_subjects()
        {
            AddNewSubject();
            Assert.Equal(_subjectByUser, _rm.SubjectIdByUserId);
        }
        [Fact]
        public void readmodel_lists_multiple_subjects_per_user()
        {
            var userId =Guid.NewGuid();
            AddNewSubject(userId);
            AddNewSubject(userId);
            Assert.Equal(_subjectByUser, _rm.SubjectIdByUserId);
        }
        private void AddSubjects(int count)
        {
            for (int i = 0; i < count; i++) { AddNewSubject(); }
        }
        private void AddNewSubject(Guid? userId = null)
        {
            var subjectId = Guid.NewGuid();
            if (userId == null) { userId = Guid.NewGuid(); }
            _subjectByUser.Add(userId.Value, subjectId);
            var evt = MessageBuilder.New(
              () => new SubjectMsgs.SubjectCreated(subjectId, userId.Value, subjectId.ToString(), authProvider, authDomain));
            var stream = _fixture.StreamNameBuilder.GenerateForAggregate(typeof(Subject), subjectId);
            _fixture.StreamStoreConnection.AppendToStream(
                stream,
                ExpectedVersion.Any,
                null,
                new[] { _fixture.EventSerializer.Serialize(evt)});
            _fixture.RepositoryEvents.WaitFor<SubjectMsgs.SubjectCreated>(TimeSpan.FromMilliseconds(100));          
            _fixture.ClearQueues();
        }
    }
}
