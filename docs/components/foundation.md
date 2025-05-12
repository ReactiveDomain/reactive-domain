# ReactiveDomain.Foundation

[← Back to Components](README.md) | [← Back to Table of Contents](../README.md)

**Component Navigation**: [← ReactiveDomain.Core](core.md) | [Next: ReactiveDomain.Messaging →](messaging.md)

The `ReactiveDomain.Foundation` component builds on the core interfaces to provide concrete implementations for domain aggregates, repositories, and other foundational elements of event sourcing.

## Table of Contents

- [Purpose and Responsibility](#purpose-and-responsibility)
- [Key Classes](#key-classes)
  - [AggregateRoot](#aggregateroot)
  - [StreamStoreRepository](#streamstorerepository)
  - [CorrelatedStreamStoreRepository](#correlatedstreamstorerepository)
- [Implementation Details](#implementation-details)
- [Usage Examples](#usage-examples)
  - [Creating an Aggregate](#creating-an-aggregate)
  - [Using Repositories](#using-repositories)
- [Integration with Other Components](#integration-with-other-components)
- [Best Practices](#best-practices)
- [Common Pitfalls](#common-pitfalls)

## Purpose and Responsibility

The primary purpose of the `ReactiveDomain.Foundation` component is to provide concrete implementations of the core interfaces defined in `ReactiveDomain.Core`. It serves as the foundation for building event-sourced applications with Reactive Domain, including:

- Aggregate roots that implement the `IEventSource` interface
- Repositories for storing and retrieving aggregates
- Support for correlation and causation tracking
- Event handling and processing

## Key Classes

### AggregateRoot

The `AggregateRoot` class is the base class for domain aggregates in Reactive Domain. It implements the `IEventSource` interface and provides common functionality for event sourcing.

```csharp
public abstract class AggregateRoot : IEventSource
{
    private readonly EventRecorder _recorder = new EventRecorder();
    
    public Guid Id { get; }
    public long ExpectedVersion { get; set; }
    
    protected AggregateRoot(Guid id)
    {
        Id = id;
        ExpectedVersion = -1;
    }
    
    protected void RaiseEvent(object @event)
    {
        _recorder.Record(@event);
        Apply(@event);
        ExpectedVersion++;
    }
    
    public void RestoreFromEvents(IEnumerable<object> events)
    {
        foreach (var @event in events)
        {
            Apply(@event);
            ExpectedVersion++;
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
    
    protected abstract void Apply(object @event);
}
```

**Key Features:**

- **Event Recording**: Automatically records events raised by the aggregate
- **State Management**: Applies events to update the aggregate's state
- **Concurrency Control**: Enforces optimistic concurrency through version checking
- **Event Sourcing**: Supports rebuilding state from events

### StreamStoreRepository

The `StreamStoreRepository` class implements the `IRepository` interface and provides a concrete implementation for storing and retrieving aggregates from an event store.

```csharp
public class StreamStoreRepository : IRepository
{
    private readonly IStreamNameBuilder _streamNameBuilder;
    private readonly IStreamStoreConnection _connection;
    private readonly IEventSerializer _serializer;
    
    public StreamStoreRepository(
        IStreamNameBuilder streamNameBuilder,
        IStreamStoreConnection connection,
        IEventSerializer serializer)
    {
        _streamNameBuilder = streamNameBuilder;
        _connection = connection;
        _serializer = serializer;
    }
    
    public bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate, int version = int.MaxValue)
        where TAggregate : class, IEventSource
    {
        // Implementation details omitted for brevity
    }
    
    public void Save(IEventSource aggregate)
    {
        // Implementation details omitted for brevity
    }
    
    public void Delete(IEventSource aggregate)
    {
        // Implementation details omitted for brevity
    }
}
```

**Key Features:**

- **Stream Naming**: Uses a stream name builder to generate consistent stream names
- **Event Serialization**: Serializes and deserializes events for storage
- **Aggregate Retrieval**: Loads aggregates by ID and version
- **Aggregate Persistence**: Saves aggregates to the event store
- **Aggregate Deletion**: Marks aggregates as deleted in the event store

### CorrelatedStreamStoreRepository

The `CorrelatedStreamStoreRepository` extends the `StreamStoreRepository` to support correlation and causation tracking.

```csharp
public class CorrelatedStreamStoreRepository : ICorrelatedRepository
{
    private readonly IRepository _innerRepository;
    
    public CorrelatedStreamStoreRepository(IRepository innerRepository)
    {
        _innerRepository = innerRepository;
    }
    
    public bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate, ICorrelatedMessage source)
        where TAggregate : AggregateRoot, IEventSource
    {
        // Implementation details omitted for brevity
    }
    
    public void Save(IEventSource aggregate)
    {
        // Implementation details omitted for brevity
    }
}
```

**Key Features:**

- **Correlation Tracking**: Tracks correlation IDs across message flows
- **Causation Tracking**: Tracks causation IDs to establish causal relationships
- **Decorator Pattern**: Decorates an existing repository to add correlation support

## Implementation Details

The `ReactiveDomain.Foundation` component is built on several key design principles:

- **Separation of Concerns**: Each class has a single responsibility
- **Composition Over Inheritance**: Uses composition to build complex behaviors
- **Immutability**: Events are treated as immutable records
- **Optimistic Concurrency**: Uses versioning to prevent conflicts

The component is designed to be:

- **Extensible**: Provides base classes and interfaces that can be extended
- **Flexible**: Supports different event store implementations and serialization formats
- **Robust**: Includes error handling and validation
- **Testable**: Designed for easy testing with minimal dependencies

## Usage Examples

### Creating an Aggregate

```csharp
public class Account : AggregateRoot
{
    private decimal _balance;
    
    public Account(Guid id) : base(id)
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
    
    protected override void Apply(object @event)
    {
        switch (@event)
        {
            case AmountDeposited e:
                _balance += e.Amount;
                break;
                
            case AmountWithdrawn e:
                _balance -= e.Amount;
                break;
        }
    }
}

public class AmountDeposited
{
    public readonly Guid AccountId;
    public readonly decimal Amount;
    
    public AmountDeposited(Guid accountId, decimal amount)
    {
        AccountId = accountId;
        Amount = amount;
    }
}

public class AmountWithdrawn
{
    public readonly Guid AccountId;
    public readonly decimal Amount;
    
    public AmountWithdrawn(Guid accountId, decimal amount)
    {
        AccountId = accountId;
        Amount = amount;
    }
}
```

### Using Repositories

```csharp
// Create an event store connection
var connectionSettings = ConnectionSettings.Create()
    .SetDefaultUserCredentials(new UserCredentials("admin", "changeit"))
    .Build();
var eventStoreConnection = new StreamStoreConnection("MyApp", connectionSettings, "localhost", 1113);
eventStoreConnection.Connect();

// Create a repository
var streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder();
var serializer = new JsonMessageSerializer();
var repository = new StreamStoreRepository(streamNameBuilder, eventStoreConnection, serializer);

// Create a correlated repository (optional)
var correlatedRepository = new CorrelatedStreamStoreRepository(repository);

// Create and save an aggregate
var accountId = Guid.NewGuid();
var account = new Account(accountId);
account.Deposit(100);
repository.Save(account);

// Load an aggregate
if (repository.TryGetById<Account>(accountId, out var loadedAccount))
{
    loadedAccount.Withdraw(50);
    repository.Save(loadedAccount);
}
```

## Integration with Other Components

The `ReactiveDomain.Foundation` component integrates with several other components in the Reactive Domain library:

- **ReactiveDomain.Core**: Implements the core interfaces defined in this component
- **ReactiveDomain.Messaging**: Uses the messaging framework for command and event handling
- **ReactiveDomain.Persistence**: Uses the persistence layer for event storage
- **ReactiveDomain.Testing**: Provides testing utilities for aggregates and repositories

## Best Practices

When working with the `ReactiveDomain.Foundation` component:

1. **Keep aggregates focused**: Each aggregate should represent a single concept in your domain
2. **Use value objects**: Use value objects to represent concepts that don't have identity
3. **Validate commands**: Validate commands before generating events
4. **Keep events simple**: Events should be simple data structures with no behavior
5. **Use correlation**: Use correlation and causation tracking for complex workflows

## Common Pitfalls

Some common issues to avoid when working with the `ReactiveDomain.Foundation` component:

1. **Large aggregates**: Avoid creating large aggregates that do too much
2. **Mutable events**: Ensure events are immutable
3. **Ignoring concurrency**: Always handle concurrency conflicts appropriately
4. **Complex event application**: Keep the logic for applying events simple
5. **Missing correlation**: Don't forget to use correlation tracking for complex workflows

---

**Component Navigation**:
- [← Previous: ReactiveDomain.Core](core.md)
- [↑ Back to Top](#reactivedomainfoundation)
- [Next: ReactiveDomain.Messaging →](messaging.md)
