# EventDrivenStateMachine

[← Back to API Reference](../README.md)

The `EventDrivenStateMachine` is the base class for event-sourced entities in Reactive Domain. It provides the core functionality for routing events, recording state changes, and managing the event history of an entity.

## Namespace

```csharp
namespace ReactiveDomain
```

## Syntax

```csharp
public abstract class EventDrivenStateMachine : IEventSource
```

## Properties

### HasRecordedEvents

Indicates whether this instance has recorded events that haven't been persisted yet.

```csharp
public bool HasRecordedEvents { get; }
```

**Returns**: `true` if there are recorded events; otherwise, `false`.

### Id

Gets the unique identifier for this event-sourced entity.

```csharp
public Guid Id { get; protected set; }
```

### Version

Gets the current version of the entity, which represents the number of events that have been applied to it.

```csharp
public long Version { get; }
```

## Methods

### RestoreFromEvents(IEnumerable\<object\>)

Restores the state of the entity from a sequence of historical events.

```csharp
public void RestoreFromEvents(IEnumerable<object> events)
```

**Parameters**:
- `events` (`System.Collections.Generic.IEnumerable<object>`): The historical events to restore from.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `events` is `null`.
- `System.InvalidOperationException`: Thrown when the entity has recorded events that haven't been persisted.

### UpdateWithEvents(IEnumerable\<object\>, long)

Updates the entity with additional events, ensuring the expected version matches.

```csharp
public void UpdateWithEvents(IEnumerable<object> events, long expectedVersion)
```

**Parameters**:
- `events` (`System.Collections.Generic.IEnumerable<object>`): The events to update with.
- `expectedVersion` (`System.Int64`): The expected version of the entity before applying these events.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `events` is `null`.
- `System.InvalidOperationException`: Thrown when the entity has no historical events or when the expected version doesn't match the current version.

### TakeEvents()

Returns all events recorded since the entity was loaded or since the last time `TakeEvents()` was called, and clears the event recorder.

```csharp
public object[] TakeEvents()
```

**Returns**: An array of objects representing the recorded events.

### Register\<TEvent\>(Action\<TEvent\>)

Registers a route for a specific type of event to the logic that needs to be applied to this instance.

```csharp
protected void Register<TEvent>(Action<TEvent> route)
```

**Type Parameters**:
- `TEvent`: The type of event.

**Parameters**:
- `route` (`System.Action<TEvent>`): The logic to route the event to.

### Register(Type, Action\<object\>)

Registers a route for a specific type of event to the logic that needs to be applied to this instance.

```csharp
protected void Register(Type typeOfEvent, Action<object> route)
```

**Parameters**:
- `typeOfEvent` (`System.Type`): The type of event.
- `route` (`System.Action<object>`): The logic to route the event to.

### Raise(object)

Raises the specified event - applies it to this instance and records it in its history.

```csharp
protected void Raise(object @event)
```

**Parameters**:
- `event` (`System.Object`): The event to apply and record.

## Protected Methods

### TakeEventStarted()

Called before the events are taken from the event recorder.

```csharp
protected virtual void TakeEventStarted()
```

### TakeEventsCompleted()

Called after the events are taken from the event recorder and it has been reset.

```csharp
protected virtual void TakeEventsCompleted()
```

### OnEventRaised(object)

Called when an event is raised.

```csharp
protected virtual void OnEventRaised(object @event)
```

**Parameters**:
- `event` (`System.Object`): The event that was raised.

## Usage

The `EventDrivenStateMachine` is the foundation for implementing event-sourced entities in Reactive Domain. It's typically not used directly but through its subclass `AggregateRoot`.

```csharp
public class Account : AggregateRoot
{
    private decimal _balance;
    
    public Account(Guid id) : base(id)
    {
        // Register event handlers
        Register<AccountCreated>(Apply);
        Register<FundsDeposited>(Apply);
        Register<FundsWithdrawn>(Apply);
    }
    
    public void Deposit(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
            
        // Raise an event - this will call Apply(FundsDeposited) and record the event
        Raise(new FundsDeposited(Id, amount));
    }
    
    private void Apply(FundsDeposited @event)
    {
        _balance += @event.Amount;
    }
    
    // Other methods and event handlers...
}
```

## Remarks

- The `EventDrivenStateMachine` implements the event sourcing pattern, where an entity's state is determined by a sequence of events.
- It uses an internal `EventRecorder` to track events that have been applied but not yet persisted.
- It uses an `EventRouter` to route events to the appropriate handler methods.
- When an event is raised using the `Raise` method, it is both applied to update the entity's state and recorded for later persistence.
- The `TakeEvents` method is typically called by a repository when persisting the entity's changes.

## See Also

- [AggregateRoot](aggregate-root.md)
- [EventRecorder](event-recorder.md)
- [IEventSource](ievent-source.md)
