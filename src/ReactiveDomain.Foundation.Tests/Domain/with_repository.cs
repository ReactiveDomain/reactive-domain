using ReactiveDomain.Audit;
using ReactiveDomain.Foundation.StreamStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Testing;
using System;
using System.Linq;
using Xunit;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation.Tests.Domain
{
    [Collection(nameof(EmbeddedStreamStoreConnectionCollection))]
    // ReSharper disable once InconsistentNaming
    public class with_repository
    {
        private readonly StreamStoreConnectionFixture _fixture;
        private readonly IRepository _repo;
        private IStreamNameBuilder _streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder();
        private IEventSerializer _serializer = new JsonMessageSerializer();
        public with_repository(StreamStoreConnectionFixture fixture)
        {
            _fixture = fixture;
            fixture.Connection.Connect();
            _repo = new StreamStoreRepository(
                _streamNameBuilder,
                fixture.Connection,
                _serializer,
                GetPolicyUserId);
        }
        private Guid GetPolicyUserId() { return _policyUserId; }
        private Guid _policyUserId = Guid.NewGuid();
        [Fact]
        public void policy_user_id_is_saved()
        {

            var id = Guid.NewGuid();
            var agg = new TestAggregate(id);
            var userId = Guid.NewGuid();
            _policyUserId = userId;
            _repo.Save(agg);


            agg.RaiseBy(10);
            var nextUser = Guid.NewGuid();
            _policyUserId = nextUser;
            _repo.Save(agg);

            var streamName = _streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), id);
            var slice = _fixture.Connection.ReadStreamForward(streamName, 0, 10);
            var evt1 = slice.Events[0];
            var newAggregate = _serializer.Deserialize(evt1) as TestAggregateMessages.NewAggregate;
            var md = newAggregate.ReadMetadatum<AuditRecord>();
            Assert.NotNull(md);
            Assert.Equal(userId, md.PolicyUserId);
            Assert.True(DateTime.UtcNow - md.EventDateUTC < TimeSpan.FromSeconds(5)); // just check for a fresh timestamp, not trying to test built in .Now 
            var evt2 = slice.Events[1];
            var incremented = _serializer.Deserialize(evt2) as TestAggregateMessages.Increment;
            var md2 = incremented.ReadMetadatum<AuditRecord>();
            Assert.NotNull(md2);
            Assert.Equal(nextUser, md2.PolicyUserId);
            Assert.True(DateTime.UtcNow - md2.EventDateUTC < TimeSpan.FromSeconds(5)); // just check for a fresh timestamp, not trying to test built in .Now 
#if NETCOREAPP
            Assert.NotEqual(md.EventDateUTC, md2.EventDateUTC);
#endif
        }
    }
}
