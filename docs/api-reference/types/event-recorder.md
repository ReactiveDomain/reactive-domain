# EventRecorder

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

The `EventRecorder` class is responsible for recording events on behalf of an event source. It's a core component of the event sourcing infrastructure in Reactive Domain, used internally by the `EventDrivenStateMachine` and `AggregateRoot` classes to track and manage domain events.

## Overview

In event-sourced systems, entities maintain their state by applying events. These events need to be recorded for persistence and later replay. The `EventRecorder` provides a simple but powerful mechanism for recording events that have been applied to an entity but not yet persisted to the event store.

When an aggregate raises an event using the `RaiseEvent()` method, the event is both applied to update the aggregate's state and recorded by the `EventRecorder`. Later, when the aggregate is saved, the recorded events are retrieved using the `TakeEvents()` method and then persisted to the event store.

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

### Within AggregateRoot

```csharp
// This is internal usage within EventDrivenStateMachine
protected void RaiseEvent(object @event)
{
    OnEventRaised(@event);
    Router.Route(@event); // Routes the event to the appropriate Apply method
    _recorder.Record(@event); // Recording the event for later persistence
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

### Custom Implementation Example

While `EventRecorder` is typically used internally, understanding how it works can be valuable for custom implementations:

```csharp
public class CustomEventSourcedEntity
{
    private readonly EventRecorder _recorder = new EventRecorder();
    private string _name;
    private bool _isActive;
    
    public Guid Id { get; }
    
    public CustomEventSourcedEntity(Guid id)
    {
        Id = id;
    }
    
    // Command handler
    public void Activate(string name)
    {
        if (_isActive)
            throw new InvalidOperationException("Already active");
            
        RaiseEvent(new EntityActivated(Id, name));
    }
    
    // Internal event raising method
    private void RaiseEvent(object @event)
    {
        Apply(@event); // Apply the event to update state
        _recorder.Record(@event); // Record for persistence
    }
    
    // Event handler
    private void Apply(EntityActivated @event)
    {
        _name = @event.Name;
        _isActive = true;
    }
    
    // Get recorded events for persistence
    public object[] GetUncommittedEvents()
    {
        var events = _recorder.RecordedEvents;
        _recorder.Reset();
        return events;
    }
}

public class EntityActivated
{
    public Guid EntityId { get; }
    public string Name { get; }
    
    public EntityActivated(Guid entityId, string name)
    {
        EntityId = entityId;
        Name = name;
    }
}
```

## Best Practices

1. **Single Responsibility**: The `EventRecorder` should only be responsible for recording events, not applying them
2. **Reset After Persistence**: Always reset the recorder after taking events for persistence
3. **Error Handling**: Implement proper error handling around event recording
4. **Thread Safety**: Be aware that `EventRecorder` is not thread-safe by default
5. **Memory Management**: Be mindful of memory usage when recording large numbers of events
6. **Event Validation**: Validate events before recording them
7. **Correlation**: Ensure events maintain proper correlation information

## Common Pitfalls

1. **Forgetting to Reset**: Failing to reset the recorder after taking events can lead to duplicate events
2. **Recording Null Events**: Attempting to record null events will throw exceptions
3. **Memory Leaks**: Holding references to large event objects can cause memory issues
4. **Circular References**: Events with circular references can cause serialization problems

## Remarks

- The `EventRecorder` is a fundamental part of the event sourcing pattern implementation in Reactive Domain.
- It maintains an in-memory list of events that have been applied to an aggregate but not yet persisted.
- When `TakeEvents()` is called on an aggregate, it retrieves all recorded events from the `EventRecorder` and then resets it.
- The `EventRecorder` is used in conjunction with the `EventRouter` to implement the full event sourcing behavior in aggregates.
- The separation of event application (via `EventRouter`) and event recording (via `EventRecorder`) follows the Single Responsibility Principle.

## Related Components

- [AggregateRoot](aggregate-root.md): Base class for domain entities that uses `EventRecorder`
- [EventDrivenStateMachine](event-driven-state-machine.md): Base class that provides event-driven behavior
- [IEventSource](ievent-source.md): Interface for event-sourced entities
- [Event](./event.md): Base class for domain events that are recorded
- [IRepository](./irepository.md): Interface for repositories that persist recorded events
- [ISnapshotSource](./isnapshot-source.md): Interface for entities that support snapshots

---

**Navigation**:
- [← Previous: ISnapshotSource](./isnapshot-source.md)
- [↑ Back to Top](#eventrecorder)
- [→ Next: EventDrivenStateMachine](./event-driven-state-machine.md)
