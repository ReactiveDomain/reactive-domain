# IEventSource Interface

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

## Overview

The `IEventSource` interface is the cornerstone of event sourcing in Reactive Domain. It represents a source of events from the perspective of restoring from and taking events, and is primarily used by infrastructure code. This interface defines the contract that all event-sourced entities must implement, providing the foundation for reconstructing entity state from a sequence of events.

In event sourcing, the state of an entity is determined by the sequence of events that have occurred, rather than by its current state. The `IEventSource` interface enables this pattern by providing methods to restore an entity from its event history, update it with new events, and extract events that have been recorded but not yet persisted. Implementations typically use private `Apply()` methods to handle specific event types and update the entity's state.

## Implementation Notes

1. The `IEventSource` interface is typically implemented by aggregate roots in a domain-driven design context
2. Implement private `Apply` methods for each event type to update the entity's state
   - These methods handle the application of events to update the entity's state
   - They are called during both event replay (via `RestoreFromEvents`) and when new events are created
   - They should be idempotent (applying the same event multiple times should not cause issues)
   - They should not have side effects like I/O operations
   - They should never raise new events to avoid infinite loops
3. Use an `EventRecorder` to track events that have been applied but not yet persisted
4. Ensure that the `ExpectedVersion` is properly maintained to support optimistic concurrency
5. When creating new events, use the `RaiseEvent()` method (in `AggregateRoot`) which both applies the event to update state and records it for persistence

**Namespace**: `ReactiveDomain`  
**Assembly**: `ReactiveDomain.Core.dll`

```csharp
public interface IEventSource
{
    Guid Id { get; }
    long ExpectedVersion { get; set; }
    void RestoreFromEvents(IEnumerable<object> events);
    void UpdateWithEvents(IEnumerable<object> events, long expectedVersion);
    object[] TakeEvents();
}
```

## Properties

### Id

Gets the unique identifier for this EventSource. This must be provided by the implementing class. The ID is used to identify the event stream associated with this entity in the event store.

```csharp
Guid Id { get; }
```

**Property Type**: `System.Guid`  
**Accessibility**: `get`

**Example**:
```csharp
public class Account : IEventSource
{
    public Guid Id { get; }
    
    public Account(Guid id)
    {
        Id = id;
    }
    
    // Implementation of other IEventSource members
}

// Usage
var account = new Account(Guid.NewGuid());
Console.WriteLine($"Account ID: {account.Id}");
```

### ExpectedVersion

Gets or sets the expected version this instance is at. This is used for optimistic concurrency control when saving the entity to an event store. The version represents the number of events that have been applied to the entity.

```csharp
long ExpectedVersion { get; set; }
```

**Property Type**: `System.Int64`  
**Accessibility**: `get`, `set`

**Example**:
```csharp
public class Account : IEventSource
{
    public Guid Id { get; }
    public long ExpectedVersion { get; set; }
    
    public Account(Guid id)
    {
        Id = id;
        ExpectedVersion = -1; // Initial version is -1, indicating no events have been applied
    }
    
    // Implementation of other IEventSource members
}

// Usage in a repository
public void Save(IEventSource aggregate)
{
    var events = aggregate.TakeEvents();
    
    // Save events to the event store, using the expected version for optimistic concurrency
    _eventStore.AppendToStream(
        aggregate.Id.ToString(), 
        aggregate.ExpectedVersion, 
        events);
        
    // Update the expected version for the next save
    aggregate.ExpectedVersion += events.Length;
}
```

## Methods

### RestoreFromEvents

Restores this instance from the history of events. This method is typically called when loading an entity from an event store, applying each event in sequence to rebuild the entity's state.

```csharp
void RestoreFromEvents(IEnumerable<object> events);
```

**Parameters**:
- `events` (`System.Collections.Generic.IEnumerable<object>`): The events to restore from.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `events` is `null`.

**Example**:
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
    
    // This is the generic Apply method that dispatches to specific event handlers
    private void Apply(object @event)
    {
        switch (@event)
        {
            case AccountCreated e:
                // Initialize account properties
                break;
                
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
    
    // Alternatively, many implementations use specific Apply methods for each event type
    // These methods are called by the generic Apply method using reflection
    private void Apply(AccountCreated @event)
    {
        // Initialize account properties
    }
    
    private void Apply(AmountDeposited @event)
    {
        _balance += @event.Amount;
    }
    
    private void Apply(AmountWithdrawn @event)
    {
        _balance -= @event.Amount;
    }
    
    // Implementation of other IEventSource members
}

// Usage in a repository
public TAggregate GetById<TAggregate>(Guid id) where TAggregate : IEventSource, new()
{
    var events = _eventStore.GetEvents(id.ToString());
    var aggregate = new TAggregate();
    
    // Set the ID property using reflection (simplified example)
    typeof(TAggregate).GetProperty("Id").SetValue(aggregate, id);
    
    // Restore the aggregate state from events
    aggregate.RestoreFromEvents(events);
    
    return aggregate;
}
```

### UpdateWithEvents

Updates this instance with the provided events, starting from the expected version. This method is used when new events need to be applied to an existing entity, such as when handling concurrent modifications.

```csharp
void UpdateWithEvents(IEnumerable<object> events, long expectedVersion);
```

**Parameters**:
- `events` (`System.Collections.Generic.IEnumerable<object>`): The events to update with.
- `expectedVersion` (`System.Int64`): The expected version to start from.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `events` is `null`.
- `System.InvalidOperationException`: Thrown when this instance does not have historical events or expected version mismatch.

**Example**:
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
    
    private void Apply(object @event)
    {
        // Apply the event to update the entity state
        // (implementation as shown in RestoreFromEvents example)
    }
    
    // Implementation of other IEventSource members
}

// Usage in a repository
public void Update<TAggregate>(ref TAggregate aggregate) where TAggregate : class, IEventSource
{
    var id = aggregate.Id;
    var currentVersion = aggregate.ExpectedVersion;
    
    // Get events from the event store that are newer than the current version
    var newEvents = _eventStore.GetEventsAfterVersion(id.ToString(), currentVersion);
    
    if (newEvents.Any())
    {
        // Update the aggregate with the new events
        aggregate.UpdateWithEvents(newEvents, currentVersion);
    }
}
```

### TakeEvents

Takes the recorded history of events from this instance (CQS violation, beware). This method returns the events that have been recorded by the entity but not yet persisted to the event store, and clears the entity's record of those events.

```csharp
object[] TakeEvents();
```

**Returns**: `System.Object[]` - The recorded events.

**Example**:
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
            
        // Record the event
        _recorder.Record(new AmountDeposited(Id, amount));
        
        // Apply the event to update the state
        _balance += amount;
    }
    
    public object[] TakeEvents()
    {
        var events = _recorder.RecordedEvents.ToArray();
        _recorder.Reset();
        return events;
    }
    
    // Implementation of other IEventSource members
}

// Usage in a repository
public void Save(IEventSource aggregate)
{
    // Extract the new events from the aggregate
    var events = aggregate.TakeEvents();
    
    if (events.Length > 0)
    {
        // Save the events to the event store
        _eventStore.AppendToStream(
            aggregate.Id.ToString(), 
            aggregate.ExpectedVersion, 
            events);
            
        // Update the expected version for the next save
        aggregate.ExpectedVersion += events.Length;
    }
}
```

## Remarks

The `IEventSource` interface is fundamental to the event sourcing pattern, where the state of an entity is determined by the sequence of events that have occurred, rather than by its current state. This approach provides several benefits:

1. **Complete Audit Trail**: Every change to the entity is recorded as an event, providing a complete history
2. **Temporal Queries**: The ability to reconstruct the entity state at any point in time
3. **Event Replay**: The ability to replay events to rebuild the entity state or to apply new business rules
4. **Event-Driven Architecture**: Natural integration with event-driven systems

Implementations of this interface typically:
1. Use an `EventRecorder` to record events
2. Implement private `Apply` methods for each event type
3. Check version consistency in `UpdateWithEvents`
4. Keep events immutable
5. Follow Command Query Separation (CQS) for methods other than `TakeEvents`

## Example Implementation

```csharp
public class Account : IEventSource
{
    private readonly EventRecorder _recorder = new EventRecorder();
    private decimal _balance;
    private string _accountNumber;
    private string _customerName;
    private bool _isClosed;
    
    public Guid Id { get; }
    public long ExpectedVersion { get; set; }
    
    public Account(Guid id)
    {
        Id = id;
        ExpectedVersion = -1;
    }
    
    // Command methods
    
    public void Create(string accountNumber, string customerName)
    {
        if (_accountNumber != null)
            throw new InvalidOperationException("Account already created");
            
        var @event = new AccountCreated(Id, accountNumber, customerName);
        _recorder.Record(@event);
        Apply(@event);
    }
    
    public void Deposit(decimal amount)
    {
        if (_isClosed)
            throw new InvalidOperationException("Cannot deposit to a closed account");
            
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
            
        var @event = new AmountDeposited(Id, amount);
        _recorder.Record(@event);
        Apply(@event);
    }
    
    public void Withdraw(decimal amount)
    {
        if (_isClosed)
            throw new InvalidOperationException("Cannot withdraw from a closed account");
            
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
            
        if (_balance < amount)
            throw new InvalidOperationException("Insufficient funds");
            
        var @event = new AmountWithdrawn(Id, amount);
        _recorder.Record(@event);
        Apply(@event);
    }
    
    public void Close()
    {
        if (_isClosed)
            throw new InvalidOperationException("Account already closed");
            
        if (_balance > 0)
            throw new InvalidOperationException("Cannot close account with positive balance");
            
        var @event = new AccountClosed(Id);
        _recorder.Record(@event);
        Apply(@event);
    }
    
    // Query methods
    
    public decimal GetBalance()
    {
        return _balance;
    }
    
    public bool IsClosed()
    {
        return _isClosed;
    }
    
    // IEventSource implementation
    
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
    
    // Event handlers
    
    private void Apply(object @event)
    {
        switch (@event)
        {
            case AccountCreated e:
                ApplyAccountCreated(e);
                break;
                
            case AmountDeposited e:
                ApplyAmountDeposited(e);
                break;
                
            case AmountWithdrawn e:
                ApplyAmountWithdrawn(e);
                break;
                
            case AccountClosed e:
                ApplyAccountClosed(e);
                break;
                
            default:
                throw new InvalidOperationException($"Unknown event type: {@event.GetType().Name}");
        }
    }
    
    private void ApplyAccountCreated(AccountCreated @event)
    {
        _accountNumber = @event.AccountNumber;
        _customerName = @event.CustomerName;
        _balance = 0;
        _isClosed = false;
    }
    
    private void ApplyAmountDeposited(AmountDeposited @event)
    {
        _balance += @event.Amount;
    }
    
    private void ApplyAmountWithdrawn(AmountWithdrawn @event)
    {
        _balance -= @event.Amount;
    }
    
    private void ApplyAccountClosed(AccountClosed @event)
    {
        _isClosed = true;
    }
}
```

## Best Practices

1. **Separate Command and Query Methods**: Keep methods that change state separate from methods that query state
2. **Immutable Events**: Design events as immutable data structures
3. **Versioning Strategy**: Plan for event schema evolution to handle changes over time
4. **Event Handlers**: Keep event handlers (Apply methods) simple and focused on updating state
5. **Optimistic Concurrency**: Use the ExpectedVersion property to prevent lost updates
6. **Error Handling**: Validate commands before recording events to ensure consistency
7. **Event Documentation**: Document the purpose and content of each event type

## Common Pitfalls

1. **Complex Event Handlers**: Avoid complex logic in event handlers that could lead to inconsistent state
2. **Side Effects in Event Handlers**: Event handlers should only update the entity state, not perform side effects
3. **Missing Version Checks**: Not checking versions when updating entities can lead to inconsistent state
4. **Event Ordering**: Be aware that events must be applied in the same order they were created
5. **Large Event Streams**: Performance can degrade with very large event streams
6. **Missing Event Handlers**: Ensure all event types have corresponding handlers

## Related Types

- [AggregateRoot](aggregate-root.md): A base class that implements `IEventSource`
- [ICorrelatedEventSource](icorrelated-event-source.md): Extends `IEventSource` with correlation tracking
- [ISnapshotSource](isnapshot-source.md): Interface for snapshot support
- [IRepository](irepository.md): Interface for repositories that work with `IEventSource`
- [EventRecorder](event-recorder.md): Utility for recording events

[↑ Back to Top](#ieventsource-interface) | [← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)
