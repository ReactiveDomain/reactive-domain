using System;

namespace ReactiveDomain.Messaging;

public record CorrelatedRoot : Event {
    public CorrelatedRoot(Guid? correlationId = null) {
        CorrelationId = correlationId ?? Guid.NewGuid();
    }
}