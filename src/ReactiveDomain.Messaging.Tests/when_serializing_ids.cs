using Newtonsoft.Json;
using Xunit;

namespace ReactiveDomain.Messaging.Tests {
    public sealed class when_serializing_ids {
        private readonly IdTestEvent _testEvent;
        private readonly IdTestEvent _childTestEvent;

        public when_serializing_ids() {
            _testEvent = new IdTestEvent(5,CorrelatedMessage.NewRoot());
            _childTestEvent = new IdTestEvent(6,_testEvent);
        }

        [Fact]
        public void can_serialize_and_recover_ids() {
            var jsonString = JsonConvert.SerializeObject(_childTestEvent, Json.JsonSettings);
            var idTesterOut = JsonConvert.DeserializeObject<IdTestEvent>(jsonString, Json.JsonSettings);
            Assert.IsType<IdTestEvent>(idTesterOut);
            Assert.Equal(_childTestEvent.CorrelationId, idTesterOut.CorrelationId);
            Assert.Equal(_childTestEvent.SourceId, idTesterOut.SourceId);
            Assert.Equal(6,_childTestEvent.Data);
        }

        [Fact]
        public void can_serialize_and_recover_nullsourceid() {
            var jsonString = JsonConvert.SerializeObject(_testEvent, Json.JsonSettings);
            var idTesterOut = JsonConvert.DeserializeObject<IdTestEvent>(jsonString, Json.JsonSettings);
            Assert.IsType<IdTestEvent>(idTesterOut);
            Assert.Equal(_testEvent.CorrelationId, idTesterOut.CorrelationId);
            Assert.Equal(_testEvent.SourceId, idTesterOut.SourceId);
            Assert.Equal(5,_testEvent.Data);
        }
    }

    public class IdTestEvent : Event
    {
        public int Data;
        //convenience
        public IdTestEvent(
            int data,
            CorrelatedMessage source) : this(data, new CorrelationId(source), new SourceId(source))
        {
        }

        [JsonConstructor]
        public IdTestEvent(
            int data, 
            CorrelationId correlationId, 
            SourceId sourceId) : base(correlationId, sourceId) {
            Data = data;
        }
    }
}
