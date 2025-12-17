using System;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Testing;

public class TestAggregateMessages {
    public record NewAggregate(Guid AggregateId) : Event;
    public record NewAggregate2(Guid AggregateId) : Event;
    public record Increment(Guid AggregateId, uint Amount) : Event;
}