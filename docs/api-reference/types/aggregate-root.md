# AggregateRoot Class

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

## Overview

The `AggregateRoot` class is a base class for domain aggregates in Reactive Domain. It implements the `IEventSource` interface and provides common functionality for event sourcing.

**Namespace**: `ReactiveDomain.Foundation`  
**Assembly**: `ReactiveDomain.Foundation.dll`

```csharp
public abstract class AggregateRoot : IEventSource
{
    protected AggregateRoot(Guid id);
    protected AggregateRoot(Guid id, ICorrelatedMessage source);
    protected AggregateRoot(Guid id, IEnumerable<object> events);
    
    public Guid Id { get; }
    public long ExpectedVersion { get; set; }
    
    protected void RaiseEvent(object @event);
    
    public void RestoreFromEvents(IEnumerable<object> events);
    public void UpdateWithEvents(IEnumerable<object> events, long expectedVersion);
    public object[] TakeEvents();
}
```

## Constructors

### AggregateRoot(Guid)

Initializes a new instance of the `AggregateRoot` class with the specified ID.

```csharp
protected AggregateRoot(Guid id);
```

**Parameters**:
- `id` (`System.Guid`): The unique identifier for the aggregate.

### AggregateRoot(Guid, ICorrelatedMessage)

Initializes a new instance of the `AggregateRoot` class with the specified ID and correlation source.

```csharp
protected AggregateRoot(Guid id, ICorrelatedMessage source);
```

**Parameters**:
- `id` (`System.Guid`): The unique identifier for the aggregate.
- `source` (`ReactiveDomain.ICorrelatedMessage`): The source message for correlation.

### AggregateRoot(Guid, IEnumerable<object>)

Initializes a new instance of the `AggregateRoot` class with the specified ID and restores it from the provided events.

```csharp
protected AggregateRoot(Guid id, IEnumerable<object> events);
```

**Parameters**:
- `id` (`System.Guid`): The unique identifier for the aggregate.
- `events` (`System.Collections.Generic.IEnumerable<object>`): The events to restore from.

## Properties

### Id

Gets the unique identifier for this aggregate.

```csharp
public Guid Id { get; }
```

**Property Type**: `System.Guid`  
**Accessibility**: `get`

### ExpectedVersion

Gets or sets the expected version this aggregate is at. This is used for optimistic concurrency control.

```csharp
public long ExpectedVersion { get; set; }
```

**Property Type**: `System.Int64`  
**Accessibility**: `get`, `set`

## Methods

### RaiseEvent

Raises an event, which will be recorded and applied to the aggregate.

```csharp
protected void RaiseEvent(object @event);
```

**Parameters**:
- `event` (`System.Object`): The event to raise.

**Remarks**: This method records the event and applies it to the aggregate by calling the appropriate `Apply` method.

### RestoreFromEvents

Restores this aggregate from the history of events.

```csharp
public void RestoreFromEvents(IEnumerable<object> events);
```

**Parameters**:
- `events` (`System.Collections.Generic.IEnumerable<object>`): The events to restore from.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `events` is `null`.

**Remarks**: This method applies each event in sequence to rebuild the aggregate's state.

### UpdateWithEvents

Updates this aggregate with the provided events, starting from the expected version.

```csharp
public void UpdateWithEvents(IEnumerable<object> events, long expectedVersion);
```

**Parameters**:
- `events` (`System.Collections.Generic.IEnumerable<object>`): The events to update with.
- `expectedVersion` (`System.Int64`): The expected version to start from.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `events` is `null`.
- `System.InvalidOperationException`: Thrown when this aggregate does not have historical events or expected version mismatch.

**Remarks**: This method checks that the expected version matches the aggregate's current version, and then applies each event in sequence.

### TakeEvents

Takes the recorded history of events from this aggregate.

```csharp
public object[] TakeEvents();
```

**Returns**: `System.Object[]` - The recorded events.

**Remarks**: This method returns the recorded events and clears the aggregate's record of those events.

## Usage

The `AggregateRoot` class is designed to be subclassed by domain aggregates. Subclasses should:

1. Define private `Apply` methods for each event type
2. Use the `RaiseEvent` method to record and apply events
3. Define public methods that represent domain operations

## Example Implementation

```csharp
public class Account : AggregateRoot
{
    private decimal _balance;
    
    // Constructor for creating a new account
    public Account(Guid id) : base(id)
    {
    }
    
    // Constructor for creating a new account with correlation
    public Account(Guid id, ICorrelatedMessage source) : base(id, source)
    {
    }
    
    // Constructor for restoring an account from events
    protected Account(Guid id, IEnumerable<object> events) : base(id, events)
    {
    }
    
    public void Deposit(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
            
        RaiseEvent(new AmountDeposited(Id, amount));
    }
    
    public void Withdraw(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
            
        if (_balance < amount)
            throw new InvalidOperationException("Insufficient funds");
            
        RaiseEvent(new AmountWithdrawn(Id, amount));
    }
    
    public decimal GetBalance()
    {
        return _balance;
    }
    
    private void Apply(AmountDeposited @event)
    {
        _balance += @event.Amount;
    }
    
    private void Apply(AmountWithdrawn @event)
    {
        _balance -= @event.Amount;
    }
}
```

## Inheritance Hierarchy

- `System.Object`
  - `ReactiveDomain.Foundation.AggregateRoot`

## Implemented Interfaces

- `ReactiveDomain.IEventSource`

## Related Types

- [IEventSource](ievent-source.md): The interface implemented by `AggregateRoot`
- [ICorrelatedEventSource](icorrelated-event-source.md): Interface for correlation tracking
- [ISnapshotSource](isnapshot-source.md): Interface for snapshot support
- [IRepository](irepository.md): Interface for repositories that work with aggregates
- [EventRecorder](event-recorder.md): Utility used internally by `AggregateRoot` to record events

[↑ Back to Top](#aggregateroot-class) | [← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)
