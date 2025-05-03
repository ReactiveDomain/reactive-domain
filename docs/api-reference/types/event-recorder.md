# EventRecorder

[← Back to API Reference](../README.md)

The `EventRecorder` class is responsible for recording events on behalf of an event source. It's a core component of the event sourcing infrastructure in Reactive Domain, used internally by the `EventDrivenStateMachine` and `AggregateRoot` classes to track and manage domain events.

## Namespace

```csharp
namespace ReactiveDomain
```

## Syntax

```csharp
public class EventRecorder
```

## Constructors

### EventRecorder()

Initializes a new instance of the `EventRecorder` class.

```csharp
public EventRecorder()
```

## Properties

### HasRecordedEvents

Indicates whether this instance has recorded events.

```csharp
public bool HasRecordedEvents { get; }
```

**Returns**: `true` if there are recorded events; otherwise, `false`.

### RecordedEvents

Gets an array containing all the events recorded by this instance.

```csharp
public object[] RecordedEvents { get; }
```

**Returns**: An array of objects representing the recorded events.

## Methods

### Record(object)

Records an event on this instance.

```csharp
public void Record(object @event)
```

**Parameters**:
- `event` (`System.Object`): The event to record.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `event` is `null`.

### Reset()

Resets this instance to its starting point or the point it was last reset on, effectively forgetting all events that have been recorded in the meantime.

```csharp
public void Reset()
```

## Usage

The `EventRecorder` is primarily used internally by the `EventDrivenStateMachine` class, which is the base class for `AggregateRoot`. It's responsible for tracking the events that have been applied to an aggregate but not yet persisted to the event store.

```csharp
// This is internal usage within EventDrivenStateMachine
protected void Raise(object @event)
{
    OnEventRaised(@event);
    Router.Route(@event);
    _recorder.Record(@event); // Recording the event
}

public object[] TakeEvents()
{
    TakeEventStarted();
    var records = _recorder.RecordedEvents; // Getting all recorded events
    _recorder.Reset(); // Clearing the recorder
    _version += records.Length;
    TakeEventsCompleted();
    return records;
}
```

## Remarks

- The `EventRecorder` is a fundamental part of the event sourcing pattern implementation in Reactive Domain.
- It maintains an in-memory list of events that have been applied to an aggregate but not yet persisted.
- When `TakeEvents()` is called on an aggregate, it retrieves all recorded events from the `EventRecorder` and then resets it.
- The `EventRecorder` is used in conjunction with the `EventRouter` to implement the full event sourcing behavior in aggregates.

## See Also

- [AggregateRoot](aggregate-root.md)
- [EventDrivenStateMachine](event-driven-state-machine.md)
- [IEventSource](ievent-source.md)
