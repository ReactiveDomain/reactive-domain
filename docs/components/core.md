# ReactiveDomain.Core

[← Back to Components](README.md) | [← Back to Table of Contents](../README.md)

The `ReactiveDomain.Core` component provides the fundamental interfaces and abstractions that form the foundation of the Reactive Domain library. These core interfaces define the contract for event sourcing and are used throughout the library.

## Table of Contents

- [Purpose and Responsibility](#purpose-and-responsibility)
- [Key Interfaces](#key-interfaces)
  - [IEventSource](#ieventsource)
  - [IMetadataSource](#imetadatasource)
- [Implementation Details](#implementation-details)
- [Usage Examples](#usage-examples)
  - [Implementing IEventSource](#implementing-ieventsource)
  - [Using IMetadataSource](#using-imetadatasource)
- [Integration with Other Components](#integration-with-other-components)
- [Best Practices](#best-practices)
- [Common Pitfalls](#common-pitfalls)

## Purpose and Responsibility

The primary purpose of the `ReactiveDomain.Core` component is to define the core abstractions for event sourcing, including:

- Event sources (entities that produce events)
- Metadata sources (entities that have associated metadata)
- Basic event handling and processing

These abstractions are deliberately minimal and focused, providing just enough structure to support the event sourcing pattern without imposing unnecessary constraints.

## Key Interfaces

### IEventSource

The `IEventSource` interface is the cornerstone of event sourcing in Reactive Domain. It represents a source of events from the perspective of restoring from and taking events.

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

**Key Responsibilities:**

- **Id**: Provides a unique identifier for the event source
- **ExpectedVersion**: Tracks the version of the event source for optimistic concurrency
- **RestoreFromEvents**: Rebuilds the state of the event source from a sequence of events
- **UpdateWithEvents**: Updates the state of the event source with new events
- **TakeEvents**: Retrieves the events that have been recorded by the event source

### IMetadataSource

The `IMetadataSource` interface defines the contract for entities that have associated metadata.

```csharp
public interface IMetadataSource
{
    Metadata ReadMetadata();
    Metadata Initialize();
    void Initialize(Metadata md);
}
```

**Key Responsibilities:**

- **ReadMetadata**: Retrieves the metadata associated with the entity
- **Initialize**: Initializes the metadata with default values or with provided metadata

## Implementation Details

The `ReactiveDomain.Core` component is intentionally lightweight, focusing on interfaces rather than implementations. The actual implementations of these interfaces are provided by other components, particularly `ReactiveDomain.Foundation`.

The core interfaces are designed to be:

- **Minimal**: Providing only the essential methods and properties
- **Focused**: Each interface has a single responsibility
- **Composable**: Interfaces can be combined to create more complex behaviors

## Usage Examples

### Implementing IEventSource

```csharp
public class MyAggregate : IEventSource
{
    private readonly EventRecorder _recorder = new EventRecorder();
    
    public Guid Id { get; }
    public long ExpectedVersion { get; set; }
    
    public MyAggregate(Guid id)
    {
        Id = id;
        ExpectedVersion = -1;
    }
    
    public void DoSomething()
    {
        // Business logic
        _recorder.Record(new SomethingDone(Id));
    }
    
    public void RestoreFromEvents(IEnumerable<object> events)
    {
        foreach (var @event in events)
        {
            Apply(@event);
        }
    }
    
    public void UpdateWithEvents(IEnumerable<object> events, long expectedVersion)
    {
        if (ExpectedVersion != expectedVersion)
            throw new InvalidOperationException("Version mismatch");
            
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
        // Apply event to update state
        // This is typically implemented using pattern matching or a dictionary of handlers
    }
}
```

### Using IMetadataSource

```csharp
public class MyMetadataEntity : IMetadataSource
{
    private Metadata _metadata;
    
    public Metadata ReadMetadata()
    {
        return _metadata ?? Initialize();
    }
    
    public Metadata Initialize()
    {
        _metadata = new Metadata();
        _metadata.Add("CreatedAt", DateTime.UtcNow);
        return _metadata;
    }
    
    public void Initialize(Metadata md)
    {
        _metadata = md;
    }
}
```

## Integration with Other Components

The `ReactiveDomain.Core` component is used by virtually all other components in the Reactive Domain library:

- **ReactiveDomain.Foundation** implements the core interfaces to provide concrete aggregates and repositories
- **ReactiveDomain.Messaging** uses the core interfaces to define message types and handlers
- **ReactiveDomain.Persistence** uses the core interfaces to define storage mechanisms for events
- **ReactiveDomain.Testing** uses the core interfaces to provide testing utilities

## Best Practices

When working with the `ReactiveDomain.Core` component:

1. **Keep implementations simple**: The core interfaces are designed to be minimal, and implementations should follow this principle
2. **Separate concerns**: Use the interfaces to create clear boundaries between different parts of your application
3. **Focus on events**: Remember that events are the central concept in event sourcing, and the core interfaces are designed to support this
4. **Use composition**: Combine interfaces to create more complex behaviors rather than creating complex inheritance hierarchies

## Common Pitfalls

Some common issues to avoid when working with the `ReactiveDomain.Core` component:

1. **Mutating events**: Events should be immutable, but the core interfaces don't enforce this
2. **Ignoring version checks**: Always check the `ExpectedVersion` to prevent concurrency issues
3. **Complex event application**: Keep the logic for applying events simple and focused
4. **Leaking implementation details**: The core interfaces should hide implementation details from clients

[↑ Back to Top](#reactivedomaincore) | [← Back to Components](README.md) | [← Back to Table of Contents](../README.md)
