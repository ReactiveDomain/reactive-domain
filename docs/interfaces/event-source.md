# IEventSource Interface

[← Back to Interfaces](README.md) | [← Back to Table of Contents](../README.md)

The `IEventSource` interface is the cornerstone of event sourcing in Reactive Domain. It represents a source of events from the perspective of restoring from and taking events, and is primarily used by infrastructure code.

## Table of Contents

- [Purpose and Responsibility](#purpose-and-responsibility)
- [Interface Definition](#interface-definition)
- [Method and Property Descriptions](#method-and-property-descriptions)
  - [Id](#id)
  - [ExpectedVersion](#expectedversion)
  - [RestoreFromEvents](#restorefromevents)
  - [UpdateWithEvents](#updatewithevents)
  - [TakeEvents](#takeevents)
- [Usage Patterns and Best Practices](#usage-patterns-and-best-practices)
  - [Implementing IEventSource](#implementing-ieventsource)
  - [Example Implementation](#example-implementation)
- [Implementation Considerations](#implementation-considerations)
- [Common Pitfalls and How to Avoid Them](#common-pitfalls-and-how-to-avoid-them)
- [Related Interfaces](#related-interfaces)
- [Conclusion](#conclusion)

## Purpose and Responsibility

The primary purpose of the `IEventSource` interface is to define the contract for entities that:

1. Can be uniquely identified
2. Maintain version information for optimistic concurrency
3. Can be restored from a sequence of events
4. Can be updated with new events
5. Can provide the events they have recorded

This interface is fundamental to the event sourcing pattern, where the state of an entity is determined by the sequence of events that have occurred, rather than by its current state.

## Interface Definition

```csharp
namespace ReactiveDomain
{
    /// <summary>
    /// Represents a source of events from the perspective of restoring from and taking events. 
    /// To be used by infrastructure code only.
    /// </summary>
    public interface IEventSource
    {
        /// <summary>
        /// Gets the unique identifier for this EventSource
        /// This must be provided by the implementing class
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// Gets or Sets the expected version this instance is at.
        /// </summary>
        long ExpectedVersion { get; set; }

        /// <summary>
        /// Restores this instance from the history of events.
        /// </summary>
        /// <param name="events">The events to restore from.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="events"/> is <c>null</c>.</exception>
        void RestoreFromEvents(IEnumerable<object> events);

        /// <summary>
        /// Updates this instance with the provided events, starting from the expected version.
        /// </summary>
        /// <param name="events">The events to update with.</param>
        /// <param name="expectedVersion">The expected version to start from.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="events"/> is <c>null</c>.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when this instance does not have historical events or expected version mismatch</exception>
        void UpdateWithEvents(IEnumerable<object> events, long expectedVersion);

        /// <summary>
        /// Takes the recorded history of events from this instance (CQS violation, beware).
        /// </summary>
        /// <returns>The recorded events.</returns>
        object[] TakeEvents();
    }
}
```

## Method and Property Descriptions

### Id

```csharp
Guid Id { get; }
```

The `Id` property provides a unique identifier for the event source. This is typically a GUID that is assigned when the entity is created. The identifier must be immutable and must be provided by the implementing class.

### ExpectedVersion

```csharp
long ExpectedVersion { get; set; }
```

The `ExpectedVersion` property tracks the version of the event source for optimistic concurrency control. It represents the version that the event source is expected to be at. When events are applied to the event source, the version is incremented. When the event source is saved to a repository, the repository checks that the expected version matches the actual version in the event store.

### RestoreFromEvents

```csharp
void RestoreFromEvents(IEnumerable<object> events);
```

The `RestoreFromEvents` method rebuilds the state of the event source from a sequence of events. This is typically called when loading an entity from a repository. The method applies each event in sequence to rebuild the entity's state.

### UpdateWithEvents

```csharp
void UpdateWithEvents(IEnumerable<object> events, long expectedVersion);
```

The `UpdateWithEvents` method updates the state of the event source with new events, starting from the expected version. This is typically called when loading additional events for an entity that has already been partially loaded. The method checks that the expected version matches the entity's current version, and then applies each event in sequence.

### TakeEvents

```csharp
object[] TakeEvents();
```

The `TakeEvents` method retrieves the events that have been recorded by the event source since it was last saved. This is typically called when saving an entity to a repository. The method returns the recorded events and clears the entity's record of those events.

## Usage Patterns and Best Practices

### Implementing IEventSource

When implementing the `IEventSource` interface, follow these best practices:

1. **Use an EventRecorder**: The `EventRecorder` class in `ReactiveDomain.Foundation` provides a convenient way to record events.

2. **Implement Apply Methods**: For each event type, implement a private `Apply` method that updates the entity's state based on the event.

3. **Check Version in UpdateWithEvents**: Always check that the expected version matches the entity's current version in the `UpdateWithEvents` method.

4. **Keep Events Immutable**: Events should be immutable value objects that represent something that happened in the past.

5. **Separate Command and Query Methods**: Follow the Command Query Separation (CQS) principle by separating methods that change state (commands) from methods that return state (queries).

### Example Implementation

```csharp
public class Account : IEventSource
{
    private readonly EventRecorder _recorder = new EventRecorder();
    private decimal _balance;
    
    public Guid Id { get; }
    public long ExpectedVersion { get; set; }
    
    public Account(Guid id)
    {
        Id = id;
        ExpectedVersion = -1;
    }
    
    public void Deposit(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
            
        _recorder.Record(new AmountDeposited(Id, amount));
    }
    
    public void Withdraw(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
            
        if (_balance < amount)
            throw new InvalidOperationException("Insufficient funds");
            
        _recorder.Record(new AmountWithdrawn(Id, amount));
    }
    
    public decimal GetBalance()
    {
        return _balance;
    }
    
    public void RestoreFromEvents(IEnumerable<object> events)
    {
        if (events == null)
            throw new ArgumentNullException(nameof(events));
            
        foreach (var @event in events)
        {
            Apply(@event);
            ExpectedVersion++;
        }
    }
    
    public void UpdateWithEvents(IEnumerable<object> events, long expectedVersion)
    {
        if (events == null)
            throw new ArgumentNullException(nameof(events));
            
        if (ExpectedVersion != expectedVersion)
            throw new InvalidOperationException($"Expected version {expectedVersion} but was {ExpectedVersion}");
            
        foreach (var @event in events)
        {
            Apply(@event);
            ExpectedVersion++;
        }
    }
    
    public object[] TakeEvents()
    {
        var events = _recorder.RecordedEvents.ToArray();
        _recorder.Reset();
        return events;
    }
    
    private void Apply(object @event)
    {
        switch (@event)
        {
            case AmountDeposited e:
                _balance += e.Amount;
                break;
                
            case AmountWithdrawn e:
                _balance -= e.Amount;
                break;
                
            default:
                throw new InvalidOperationException($"Unknown event type: {@event.GetType().Name}");
        }
    }
}
```

## Implementation Considerations

When implementing the `IEventSource` interface, consider the following:

1. **Event Application Logic**: The logic for applying events should be simple and focused on updating the entity's state.

2. **Version Management**: The `ExpectedVersion` property is critical for optimistic concurrency control. Make sure it's properly managed.

3. **Event Recording**: Use the `EventRecorder` class to record events, rather than implementing this functionality yourself.

4. **Event Types**: Events should be simple value objects that represent something that happened in the past. They should be immutable and contain all the data needed to understand what happened.

5. **Performance**: The `RestoreFromEvents` and `UpdateWithEvents` methods may be called with a large number of events. Make sure they're efficient.

## Common Pitfalls and How to Avoid Them

1. **Mutating Events**: Events should be immutable, but the `IEventSource` interface doesn't enforce this. Make sure your events are immutable.

2. **Ignoring Version Checks**: Always check the `ExpectedVersion` in the `UpdateWithEvents` method to prevent concurrency issues.

3. **Complex Event Application**: Keep the logic for applying events simple and focused. Avoid complex business logic in the `Apply` methods.

4. **Leaking Implementation Details**: The `IEventSource` interface should hide implementation details from clients. Don't expose internal state or behavior.

5. **Not Clearing Events**: The `TakeEvents` method should clear the entity's record of events after returning them. Otherwise, the same events may be saved multiple times.

## Related Interfaces

- [IRepository](repository.md): Provides methods for loading and saving event sources.
- [ICorrelatedEventSource](correlated-event-source.md): Extends `IEventSource` with correlation tracking.
- [ISnapshotSource](snapshot-source.md): Provides methods for creating and restoring from snapshots.

## Conclusion

The `IEventSource` interface is the foundation of event sourcing in Reactive Domain. By implementing this interface, entities can participate in the event sourcing pattern, where state is determined by the sequence of events that have occurred. This enables powerful features like complete audit trails, temporal queries, and event replay.

[↑ Back to Top](#ieventsource-interface) | [← Back to Interfaces](README.md) | [← Back to Table of Contents](../README.md)
