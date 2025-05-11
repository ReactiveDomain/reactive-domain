# ICorrelatedRepository Interface

[← Back to Interfaces](README.md)

The `ICorrelatedRepository` interface extends the standard [IRepository](repository.md) interface with correlation tracking capabilities. It allows for tracking the flow of messages through the system by maintaining correlation and causation IDs across command and event boundaries.

## Interface Definition

```csharp
public interface ICorrelatedRepository : IRepository
{
    bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate, ICorrelatedMessage source) 
        where TAggregate : AggregateRoot, IEventSource;
        
    TAggregate GetById<TAggregate>(Guid id, ICorrelatedMessage source) 
        where TAggregate : AggregateRoot, IEventSource;
}
```

## Methods

### TryGetById with Correlation

```csharp
bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate, ICorrelatedMessage source) 
    where TAggregate : AggregateRoot, IEventSource;
```

Attempts to retrieve an aggregate by its ID, using the provided correlated message as the source for correlation tracking.

**Parameters:**
- `id`: The unique identifier of the aggregate to retrieve.
- `aggregate`: When this method returns, contains the aggregate associated with the specified ID, if the aggregate is found; otherwise, the default value for the type of the aggregate parameter.
- `source`: The source message that contains correlation and causation IDs.

**Returns:**
- `true` if the aggregate is found; otherwise, `false`.

**Type Parameters:**
- `TAggregate`: The type of the aggregate to retrieve. Must be a subclass of `AggregateRoot` and implement `IEventSource`.

**Example:**
```csharp
if (repository.TryGetById<Account>(command.AccountId, out var account, command))
{
    // Account found, use it
    account.Deposit(command.Amount, command.Reference, command);
    repository.Save(account);
}
else
{
    // Account not found, handle the case
    throw new AccountNotFoundException(command.AccountId);
}
```

### GetById with Correlation

```csharp
TAggregate GetById<TAggregate>(Guid id, ICorrelatedMessage source) 
    where TAggregate : AggregateRoot, IEventSource;
```

Retrieves an aggregate by its ID, using the provided correlated message as the source for correlation tracking. Throws an exception if the aggregate is not found.

**Parameters:**
- `id`: The unique identifier of the aggregate to retrieve.
- `source`: The source message that contains correlation and causation IDs.

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
    var account = repository.GetById<Account>(command.AccountId, command);
    account.Deposit(command.Amount, command.Reference, command);
    repository.Save(account);
}
catch (AggregateNotFoundException ex)
{
    // Handle the case where the account is not found
    Console.WriteLine($"Account not found: {ex.Message}");
}
```

## Correlation Tracking

The `ICorrelatedRepository` interface is designed to work with the [ICorrelatedMessage](correlated-message.md) interface to provide end-to-end correlation tracking. When an aggregate is loaded using a correlated message as the source, the correlation and causation IDs are propagated to any events raised by the aggregate.

This enables tracking the flow of messages through the system, which is particularly useful for:

1. **Debugging**: Tracing the flow of messages through the system to identify issues.
2. **Auditing**: Tracking who initiated a particular action and when.
3. **Distributed Tracing**: Tracking messages across service boundaries in a distributed system.
4. **Causality Tracking**: Understanding the cause-and-effect relationships between messages.

## Implementation Considerations

When implementing the `ICorrelatedRepository` interface, consider the following:

1. **Correlation Propagation**: Ensure that correlation and causation IDs are properly propagated from the source message to any events raised by the aggregate.
2. **Concurrency Control**: Implement optimistic concurrency control to handle concurrent modifications to the same aggregate.
3. **Event Metadata**: Store correlation and causation IDs in the event metadata for later retrieval.
4. **Error Handling**: Implement proper error handling for event store communication issues.

## Common Implementations

### CorrelatedStreamStoreRepository

The `CorrelatedStreamStoreRepository` is the standard implementation of `ICorrelatedRepository` that uses EventStoreDB as the underlying event store.

```csharp
public class CorrelatedStreamStoreRepository : ICorrelatedRepository
{
    private readonly IStreamStoreConnection _connection;
    private readonly ISnapshotStore _snapshotStore;
    private readonly ISnapshotStrategy _snapshotStrategy;
    
    public CorrelatedStreamStoreRepository(
        IStreamStoreConnection connection,
        ISnapshotStore snapshotStore = null,
        ISnapshotStrategy snapshotStrategy = null)
    {
        _connection = connection;
        _snapshotStore = snapshotStore;
        _snapshotStrategy = snapshotStrategy;
    }
    
    // Implementation of ICorrelatedRepository methods
}
```

## Related Interfaces

- [IRepository](repository.md): The base repository interface.
- [ICorrelatedMessage](correlated-message.md): Interface for messages with correlation tracking.
- [ICorrelatedEventSource](correlated-event-source.md): Interface for event sources with correlation tracking.
- [IEventSource](event-source.md): The core interface for event-sourced entities.

## Best Practices

1. **Always Use Correlation**: Use `ICorrelatedRepository` instead of `IRepository` when correlation tracking is needed.
2. **Pass the Original Command**: Always pass the original command as the source message to maintain the correlation chain.
3. **Use MessageBuilder**: Use the `MessageBuilder` class to create correlated messages.
4. **Include Correlation in Logging**: Include correlation IDs in log messages for easier debugging.
5. **Monitor Correlation Chains**: Implement monitoring for correlation chains to identify issues.

## Example Usage in a Command Handler

```csharp
public class TransferFundsHandler : ICommandHandler<TransferFunds>
{
    private readonly ICorrelatedRepository _repository;
    private readonly IEventBus _eventBus;
    
    public TransferFundsHandler(
        ICorrelatedRepository repository,
        IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }
    
    public void Handle(TransferFunds command)
    {
        // Load source account with correlation
        var sourceAccount = _repository.GetById<Account>(command.SourceAccountId, command);
        
        // Load target account with correlation
        var targetAccount = _repository.GetById<Account>(command.TargetAccountId, command);
        
        // Execute transfer with correlation
        sourceAccount.Withdraw(command.Amount, $"Transfer to {command.TargetAccountId}", command);
        targetAccount.Deposit(command.Amount, $"Transfer from {command.SourceAccountId}", command);
        
        // Save both accounts
        _repository.Save(sourceAccount);
        _repository.Save(targetAccount);
        
        // Publish transfer completed event with correlation
        _eventBus.Publish(MessageBuilder.From(command, () => new TransferCompleted(
            command.SourceAccountId,
            command.TargetAccountId,
            command.Amount,
            DateTime.UtcNow)));
    }
}
```

## Navigation

**Section Navigation**:
- [← Previous: IRepository](repository.md)
- [↑ Parent: Interfaces](README.md)
- [→ Next: IListener](listener.md)

**Quick Links**:
- [Home](../README.md)
- [Core Concepts](../core-concepts.md)
- [API Reference](../api-reference/README.md)
- [Code Examples](../code-examples/README.md)
- [Troubleshooting](../troubleshooting.md)

---

*This documentation is part of the [Reactive Domain](https://github.com/ReactiveDomain/reactive-domain) project.*
