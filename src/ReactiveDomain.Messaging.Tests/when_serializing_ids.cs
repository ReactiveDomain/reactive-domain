using System;
using Newtonsoft.Json;
using Xunit;

namespace ReactiveDomain.Messaging.Tests
{
    public sealed class when_serializing_ids
    {
        private readonly CorrelationId _corrId;
        private readonly SourceId _sourceId;
        private readonly SourceId _nullSourceId;

        public when_serializing_ids()
        {
            _corrId = CorrelationId.NewId();
            _sourceId = new SourceId(Guid.NewGuid());
            _nullSourceId = SourceId.NullSourceId();
        }

        [Fact]
        public void can_serialize_and_recover_ids()
        {
            var idTester = new IdTester(_corrId, _sourceId);
            var jsonString = JsonConvert.SerializeObject(idTester, Json.JsonSettings);
            var idTesterOut = JsonConvert.DeserializeObject<IdTester>(jsonString, Json.JsonSettings);
            Assert.IsType<IdTester>(idTesterOut);
            Assert.Equal(_corrId, idTester.CorrId);
            Assert.Equal(_sourceId, idTester.SrcId);
        }

        [Fact]
        public void can_serialize_and_recover_nullsourceid()
        {
            var idTester = new IdTester(_corrId, _nullSourceId);
            var jsonString = JsonConvert.SerializeObject(idTester, Json.JsonSettings);
            var idTesterOut = JsonConvert.DeserializeObject<IdTester>(jsonString, Json.JsonSettings);
            Assert.IsType<IdTester>(idTesterOut);
            Assert.Equal(_corrId, idTester.CorrId);
            Assert.Equal(_nullSourceId, idTester.SrcId);
        }
    }

    public class IdTester
    {
        public readonly CorrelationId CorrId;
        public readonly SourceId SrcId;

        public IdTester(
            CorrelationId correlationId,
            SourceId sourceId)
        {
            CorrId = correlationId;
            SrcId = sourceId;
        }
    }
}
