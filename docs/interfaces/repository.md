# IRepository Interface

[← Back to Interfaces](README.md)

The `IRepository` interface is a core component of the Reactive Domain library, implementing the Repository pattern for event-sourced aggregates. It provides methods for loading and saving aggregates from and to an event store.

## Interface Definition

```csharp
public interface IRepository
{
    bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate) 
        where TAggregate : AggregateRoot, IEventSource;
        
    TAggregate GetById<TAggregate>(Guid id) 
        where TAggregate : AggregateRoot, IEventSource;
        
    void Save<TAggregate>(TAggregate aggregate) 
        where TAggregate : AggregateRoot, IEventSource;
}
```

## Methods

### TryGetById

```csharp
bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate) 
    where TAggregate : AggregateRoot, IEventSource;
```

Attempts to retrieve an aggregate by its ID. Returns `true` if the aggregate is found, `false` otherwise.

**Parameters:**
- `id`: The unique identifier of the aggregate to retrieve.
- `aggregate`: When this method returns, contains the aggregate associated with the specified ID, if the aggregate is found; otherwise, the default value for the type of the aggregate parameter.

**Returns:**
- `true` if the aggregate is found; otherwise, `false`.

**Type Parameters:**
- `TAggregate`: The type of the aggregate to retrieve. Must be a subclass of `AggregateRoot` and implement `IEventSource`.

**Example:**
```csharp
if (repository.TryGetById<Account>(accountId, out var account))
{
    // Account found, use it
    account.Deposit(amount);
    repository.Save(account);
}
else
{
    // Account not found, handle the case
    throw new AccountNotFoundException(accountId);
}
```

### GetById

```csharp
TAggregate GetById<TAggregate>(Guid id) 
    where TAggregate : AggregateRoot, IEventSource;
```

Retrieves an aggregate by its ID. Throws an exception if the aggregate is not found.

**Parameters:**
- `id`: The unique identifier of the aggregate to retrieve.

**Returns:**
- The aggregate associated with the specified ID.

**Type Parameters:**
- `TAggregate`: The type of the aggregate to retrieve. Must be a subclass of `AggregateRoot` and implement `IEventSource`.

**Exceptions:**
- `AggregateNotFoundException`: Thrown when the aggregate with the specified ID is not found.

**Example:**
```csharp
try
{
    var account = repository.GetById<Account>(accountId);
    account.Deposit(amount);
    repository.Save(account);
}
catch (AggregateNotFoundException ex)
{
    // Handle the case where the account is not found
    Console.WriteLine($"Account not found: {ex.Message}");
}
```

### Save

```csharp
void Save<TAggregate>(TAggregate aggregate) 
    where TAggregate : AggregateRoot, IEventSource;
```

Saves an aggregate to the event store.

**Parameters:**
- `aggregate`: The aggregate to save.

**Type Parameters:**
- `TAggregate`: The type of the aggregate to save. Must be a subclass of `AggregateRoot` and implement `IEventSource`.

**Exceptions:**
- `ConcurrencyException`: Thrown when there is a concurrency conflict during the save operation.
- `EventStoreException`: Thrown when there is an error communicating with the event store.

**Example:**
```csharp
var account = repository.GetById<Account>(accountId);
account.Deposit(amount);
repository.Save(account);
```

## Implementation Considerations

When implementing the `IRepository` interface, consider the following:

1. **Concurrency Control**: Implement optimistic concurrency control to handle concurrent modifications to the same aggregate.
2. **Event Serialization**: Ensure proper serialization and deserialization of events.
3. **Snapshot Support**: Consider implementing snapshot support for performance optimization.
4. **Caching**: Implement caching to improve performance for frequently accessed aggregates.
5. **Error Handling**: Implement proper error handling for event store communication issues.

## Common Implementations

### StreamStoreRepository

The `StreamStoreRepository` is the standard implementation of `IRepository` that uses EventStoreDB as the underlying event store.

```csharp
public class StreamStoreRepository : IRepository
{
    private readonly IStreamStoreConnection _connection;
    private readonly ISnapshotStore _snapshotStore;
    private readonly ISnapshotStrategy _snapshotStrategy;
    
    public StreamStoreRepository(
        IStreamStoreConnection connection,
        ISnapshotStore snapshotStore = null,
        ISnapshotStrategy snapshotStrategy = null)
    {
        _connection = connection;
        _snapshotStore = snapshotStore;
        _snapshotStrategy = snapshotStrategy;
    }
    
    // Implementation of IRepository methods
}
```

### InMemoryRepository

The `InMemoryRepository` is an in-memory implementation of `IRepository` used primarily for testing.

```csharp
public class InMemoryRepository : IRepository
{
    private readonly Dictionary<Guid, List<object>> _eventStore = new Dictionary<Guid, List<object>>();
    
    // Implementation of IRepository methods
}
```

## Related Interfaces

- [IEventSource](event-source.md): The core interface for event-sourced entities.
- [ICorrelatedRepository](correlated-repository.md): Extends `IRepository` with correlation support.
- [ISnapshotSource](snapshot-source.md): Interface for snapshot support.

## Best Practices

1. **Use Dependency Injection**: Inject the repository into your services and command handlers.
2. **Handle Concurrency Exceptions**: Implement retry logic for concurrency exceptions.
3. **Use TryGetById When Appropriate**: Use `TryGetById` when the aggregate might not exist to avoid exceptions.
4. **Consider Performance**: Use snapshots for large aggregates to improve performance.
5. **Implement Unit Tests**: Write comprehensive unit tests for your repository implementations.

## Example Usage in a Command Handler

```csharp
public class DepositFundsHandler : ICommandHandler<DepositFunds>
{
    private readonly IRepository _repository;
    
    public DepositFundsHandler(IRepository repository)
    {
        _repository = repository;
    }
    
    public void Handle(DepositFunds command)
    {
        var account = _repository.GetById<Account>(command.AccountId);
        account.Deposit(command.Amount);
        _repository.Save(account);
    }
}
```

## Navigation

**Section Navigation**:
- [← Previous: IEventSource](event-source.md)
- [↑ Parent: Interfaces](README.md)
- [→ Next: ICorrelatedRepository](correlated-repository.md)

**Quick Links**:
- [Home](../README.md)
- [Core Concepts](../core-concepts.md)
- [API Reference](../api-reference/README.md)
- [Code Examples](../code-examples/README.md)
- [Troubleshooting](../troubleshooting.md)

---

*This documentation is part of the [Reactive Domain](https://github.com/ReactiveDomain/reactive-domain) project.*
