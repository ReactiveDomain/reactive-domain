using System;
using Newtonsoft.Json;
using ReactiveDomain.Messaging;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation.Tests {
    public class CorrelatedAggregate : CorrelatedEDSM {
        //reflection based constructor
        public CorrelatedAggregate() {
            Register<Created>(evt => Id = evt.Id);
        }

        public CorrelatedAggregate(
                                Guid id,
                                CorrelatedMessage source) : this() {
            Source = source; //n.b. source is a transient value used to set the context for the events
            Raise(new Created(id, source.CorrelationId, new SourceId(source)));
        }

        public class Created : Event {
            public readonly Guid Id;
            public Created(
                Guid id,
                CorrelationId correlationId,
                SourceId sourceId) :
                base(correlationId, sourceId) {
                Id = id;
            }
        }

        public class CorrelatedEvent : Event {
            public CorrelatedEvent(CorrelatedMessage source) : base(source) {

            }
            [JsonConstructor]
            public CorrelatedEvent(
                CorrelationId correlationId,
                SourceId sourceId) :
                    base(correlationId, sourceId) {
            }

        }
        public class UncorrelatedEvent : Message { }

        public void RaiseCorrelatedEvent() {
            Raise(new CorrelatedEvent(Source));
        }
        public void RaiseCorrelatedEventByIds() {
            Raise(new CorrelatedEvent(Source.CorrelationId, new SourceId(Source)));
        }

        public void RaiseUncorrelatedEvent() {
            Raise(new UncorrelatedEvent());
        }
        public void RaiseExternallyCorrelatedEvent(CorrelatedMessage source) {
            Raise(new CorrelatedEvent(source));
        }
        public void RaiseExternallyCorrelatedEvent(CorrelationId correlationId,
                                                   SourceId sourceId) {
            Raise(new CorrelatedEvent(correlationId, sourceId));
        }
    }
}
