using Newtonsoft.Json;
using System;
using Xunit;

namespace ReactiveDomain.Messaging.Tests
{
    public sealed class when_serializing_ids
    {
        private readonly IdTestEvent _testEvent;
        private readonly IdTestEvent _childTestEvent;

        public when_serializing_ids()
        {
           
            _testEvent =  MessageBuilder
                                .New(()=> new IdTestEvent(5));
            _childTestEvent = MessageBuilder
                                .From(_testEvent)
                                .Build(()=>new IdTestEvent(6));
        }

        [Fact]
        public void can_serialize_and_recover_ids()
        {
            var jsonString = JsonConvert.SerializeObject(_childTestEvent, Json.JsonSettings);
            var idTesterOut = JsonConvert.DeserializeObject<IdTestEvent>(jsonString, Json.JsonSettings);
            Assert.IsType<IdTestEvent>(idTesterOut);
            Assert.Equal(_childTestEvent.CorrelationId, idTesterOut.CorrelationId);
            Assert.Equal(_childTestEvent.CausationId, idTesterOut.CausationId);
            Assert.Equal(6, _childTestEvent.Data);
        }

        [Fact]
        public void can_serialize_and_recover_nullsourceid()
        {
            var jsonString = JsonConvert.SerializeObject(_testEvent, Json.JsonSettings);
            var idTesterOut = JsonConvert.DeserializeObject<IdTestEvent>(jsonString, Json.JsonSettings);
            Assert.IsType<IdTestEvent>(idTesterOut);
            Assert.Equal(_testEvent.CorrelationId, idTesterOut.CorrelationId);
            Assert.Equal(_testEvent.CausationId, idTesterOut.CausationId);
            Assert.Equal(5, _testEvent.Data);
        }
    }

    public class IdTestEvent : ICorrelatedMessage
    {
        public Guid MsgId { get; }
        public Guid CorrelationId { get; set; }
        public Guid CausationId { get; set; }
        public int Data;
        //convenience
        public IdTestEvent(
            int data)
        {
            Data = data;
        }       
    }
}
