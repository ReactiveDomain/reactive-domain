# IEventSource Interface

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

## Overview

The `IEventSource` interface is the cornerstone of event sourcing in Reactive Domain. It represents a source of events from the perspective of restoring from and taking events, and is primarily used by infrastructure code.

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

Gets the unique identifier for this EventSource. This must be provided by the implementing class.

```csharp
Guid Id { get; }
```

**Property Type**: `System.Guid`  
**Accessibility**: `get`

### ExpectedVersion

Gets or sets the expected version this instance is at. This is used for optimistic concurrency control.

```csharp
long ExpectedVersion { get; set; }
```

**Property Type**: `System.Int64`  
**Accessibility**: `get`, `set`

## Methods

### RestoreFromEvents

Restores this instance from the history of events.

```csharp
void RestoreFromEvents(IEnumerable<object> events);
```

**Parameters**:
- `events` (`System.Collections.Generic.IEnumerable<object>`): The events to restore from.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `events` is `null`.

### UpdateWithEvents

Updates this instance with the provided events, starting from the expected version.

```csharp
void UpdateWithEvents(IEnumerable<object> events, long expectedVersion);
```

**Parameters**:
- `events` (`System.Collections.Generic.IEnumerable<object>`): The events to update with.
- `expectedVersion` (`System.Int64`): The expected version to start from.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `events` is `null`.
- `System.InvalidOperationException`: Thrown when this instance does not have historical events or expected version mismatch.

### TakeEvents

Takes the recorded history of events from this instance (CQS violation, beware).

```csharp
object[] TakeEvents();
```

**Returns**: `System.Object[]` - The recorded events.

## Remarks

The `IEventSource` interface is fundamental to the event sourcing pattern, where the state of an entity is determined by the sequence of events that have occurred, rather than by its current state.

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

## Related Types

- [AggregateRoot](aggregate-root.md): A base class that implements `IEventSource`
- [ICorrelatedEventSource](icorrelated-event-source.md): Extends `IEventSource` with correlation tracking
- [ISnapshotSource](isnapshot-source.md): Interface for snapshot support
- [IRepository](irepository.md): Interface for repositories that work with `IEventSource`
- [EventRecorder](event-recorder.md): Utility for recording events

[↑ Back to Top](#ieventsource-interface) | [← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)
