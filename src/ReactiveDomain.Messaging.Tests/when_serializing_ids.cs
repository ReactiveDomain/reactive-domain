using System.Threading;
using Newtonsoft.Json;
using Xunit;

namespace ReactiveDomain.Messaging.Tests
{
    public sealed class when_serializing_ids
    {
        private readonly IdTestEvent _testEvent;
        private readonly IdTestEvent _childTestEvent;

        public when_serializing_ids()
        {
            _testEvent = new IdTestEvent(CorrelationId.NewId(), SourceId.NullSourceId());
            _childTestEvent = new IdTestEvent(new CorrelationId(_testEvent), new SourceId(_testEvent));
        }

        [Fact]
        public void can_serialize_and_recover_ids()
        {
            var jsonString = JsonConvert.SerializeObject(_childTestEvent, Json.JsonSettings);
            var idTesterOut = JsonConvert.DeserializeObject<IdTestEvent>(jsonString, Json.JsonSettings);
            Assert.IsType<IdTestEvent>(idTesterOut);
            Assert.Equal(_childTestEvent.CorrelationId, idTesterOut.CorrelationId);
            Assert.Equal(_childTestEvent.SourceId, idTesterOut.SourceId);
        }

        [Fact]
        public void can_serialize_and_recover_nullsourceid()
        {
            var jsonString = JsonConvert.SerializeObject(_testEvent, Json.JsonSettings);
            var idTesterOut = JsonConvert.DeserializeObject<IdTestEvent>(jsonString, Json.JsonSettings);
            Assert.IsType<IdTestEvent>(idTesterOut);
            Assert.Equal(_testEvent.CorrelationId, idTesterOut.CorrelationId);
            Assert.Equal(_testEvent.SourceId, idTesterOut.SourceId);
        }
    }

    public class IdTestEvent : Event
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;

        public IdTestEvent(
            CorrelationId correlationId,
            SourceId sourceId)
            : base(correlationId, sourceId)
        {
        }
    }
}
