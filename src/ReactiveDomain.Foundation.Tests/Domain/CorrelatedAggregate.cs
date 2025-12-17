using System;
using ReactiveDomain.Messaging;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation.Tests.Domain;

public class CorrelatedAggregate : AggregateRoot {
    //reflection based constructor
    public CorrelatedAggregate() {
        Register<Created>(evt => Id = evt.Id);
    }


    public CorrelatedAggregate(
        Guid id,
        ICorrelatedMessage source) : this() {
        ((ICorrelatedEventSource)this).Source = source;
        Raise(new Created(id));
    }

    public record Created(Guid Id) : Event;

    public record CorrelatedEvent : Event;
    public record UncorrelatedEvent : Message;

    public void RaiseCorrelatedEvent() {
        Raise(new CorrelatedEvent());
    }

    public void RaiseUncorrelatedEvent() {
        Raise(new UncorrelatedEvent());
    }
    public void RaiseExternallyCorrelatedEvent(ICorrelatedMessage source) {
        Raise(new CorrelatedEvent() {
            CorrelationId = source.CorrelationId,
            CausationId = source.MsgId
        });
    }
    public void RaiseExternallyCorrelatedEvent(Guid correlationId,
        Guid sourceMsgId) {
        Raise(new CorrelatedEvent() {
            CorrelationId = correlationId,
            CausationId = sourceMsgId
        });
    }
}