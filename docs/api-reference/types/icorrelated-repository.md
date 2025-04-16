# ICorrelatedRepository Interface

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

## Overview

The `ICorrelatedRepository` interface extends the repository pattern with correlation support. It allows tracking correlation and causation IDs across message flows when working with event-sourced aggregates.

**Namespace**: `ReactiveDomain.Foundation`  
**Assembly**: `ReactiveDomain.Foundation.dll`

```csharp
public interface ICorrelatedRepository
{
    bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
    bool TryGetById<TAggregate>(Guid id, int version, out TAggregate aggregate, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
    TAggregate GetById<TAggregate>(Guid id, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
    TAggregate GetById<TAggregate>(Guid id, int version, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
    void Save(IEventSource aggregate);
    void Delete(IEventSource aggregate);
    void HardDelete(IEventSource aggregate);
}
```

## Methods

### TryGetById<TAggregate>(Guid, out TAggregate, ICorrelatedMessage)

Attempts to retrieve an aggregate by its ID with correlation information.

```csharp
bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
```

**Type Parameters**:
- `TAggregate`: The type of the aggregate to retrieve.

**Parameters**:
- `id` (`System.Guid`): The ID of the aggregate to retrieve.
- `aggregate` (`TAggregate`): When this method returns, contains the aggregate with the specified ID, if found; otherwise, the default value for the type of the `aggregate` parameter.
- `source` (`ReactiveDomain.ICorrelatedMessage`): The source message for correlation.

**Returns**: `System.Boolean` - `true` if the aggregate was found; otherwise, `false`.

**Remarks**: This method attempts to retrieve an aggregate by its ID and sets up correlation information. If the aggregate is not found, it returns `false` and sets `aggregate` to `null`.

### TryGetById<TAggregate>(Guid, int, out TAggregate, ICorrelatedMessage)

Attempts to retrieve an aggregate by its ID and version with correlation information.

```csharp
bool TryGetById<TAggregate>(Guid id, int version, out TAggregate aggregate, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
```

**Type Parameters**:
- `TAggregate`: The type of the aggregate to retrieve.

**Parameters**:
- `id` (`System.Guid`): The ID of the aggregate to retrieve.
- `version` (`System.Int32`): The version of the aggregate to retrieve.
- `aggregate` (`TAggregate`): When this method returns, contains the aggregate with the specified ID and version, if found; otherwise, the default value for the type of the `aggregate` parameter.
- `source` (`ReactiveDomain.ICorrelatedMessage`): The source message for correlation.

**Returns**: `System.Boolean` - `true` if the aggregate was found; otherwise, `false`.

**Remarks**: This method attempts to retrieve an aggregate by its ID and version and sets up correlation information. If the aggregate is not found, it returns `false` and sets `aggregate` to `null`.

### GetById<TAggregate>(Guid, ICorrelatedMessage)

Retrieves an aggregate by its ID with correlation information.

```csharp
TAggregate GetById<TAggregate>(Guid id, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
```

**Type Parameters**:
- `TAggregate`: The type of the aggregate to retrieve.

**Parameters**:
- `id` (`System.Guid`): The ID of the aggregate to retrieve.
- `source` (`ReactiveDomain.ICorrelatedMessage`): The source message for correlation.

**Returns**: `TAggregate` - The aggregate with the specified ID.

**Exceptions**:
- `ReactiveDomain.AggregateNotFoundException`: Thrown when the aggregate with the specified ID is not found.
- `ReactiveDomain.AggregateDeletedException`: Thrown when the aggregate with the specified ID has been deleted.

**Remarks**: This method retrieves an aggregate by its ID and sets up correlation information. If the aggregate is not found, it throws an exception.

### GetById<TAggregate>(Guid, int, ICorrelatedMessage)

Retrieves an aggregate by its ID and version with correlation information.

```csharp
TAggregate GetById<TAggregate>(Guid id, int version, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
```

**Type Parameters**:
- `TAggregate`: The type of the aggregate to retrieve.

**Parameters**:
- `id` (`System.Guid`): The ID of the aggregate to retrieve.
- `version` (`System.Int32`): The version of the aggregate to retrieve.
- `source` (`ReactiveDomain.ICorrelatedMessage`): The source message for correlation.

**Returns**: `TAggregate` - The aggregate with the specified ID and version.

**Exceptions**:
- `ReactiveDomain.AggregateNotFoundException`: Thrown when the aggregate with the specified ID is not found.
- `ReactiveDomain.AggregateDeletedException`: Thrown when the aggregate with the specified ID has been deleted.
- `ReactiveDomain.AggregateVersionException`: Thrown when the specified version does not match the expected version.

**Remarks**: This method retrieves an aggregate by its ID and version and sets up correlation information. If the aggregate is not found, it throws an exception.

### Save

Saves an aggregate to the repository.

```csharp
void Save(IEventSource aggregate);
```

**Parameters**:
- `aggregate` (`ReactiveDomain.IEventSource`): The aggregate to save.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `aggregate` is `null`.
- `ReactiveDomain.AggregateVersionException`: Thrown when the aggregate's expected version does not match the version in the repository.

**Remarks**: This method saves an aggregate to the repository. It takes the events from the aggregate and appends them to the event stream in the repository.

### Delete

Marks an aggregate as deleted in the repository.

```csharp
void Delete(IEventSource aggregate);
```

**Parameters**:
- `aggregate` (`ReactiveDomain.IEventSource`): The aggregate to delete.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `aggregate` is `null`.
- `ReactiveDomain.AggregateVersionException`: Thrown when the aggregate's expected version does not match the version in the repository.

**Remarks**: This method marks an aggregate as deleted in the repository. It appends a deletion event to the event stream. The aggregate can still be retrieved, but will be marked as deleted.

### HardDelete

Permanently deletes an aggregate from the repository.

```csharp
void HardDelete(IEventSource aggregate);
```

**Parameters**:
- `aggregate` (`ReactiveDomain.IEventSource`): The aggregate to delete.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `aggregate` is `null`.
- `ReactiveDomain.AggregateVersionException`: Thrown when the aggregate's expected version does not match the version in the repository.

**Remarks**: This method permanently deletes an aggregate from the repository. It removes the event stream from the repository. The aggregate cannot be retrieved after this operation.

## Usage

The `ICorrelatedRepository` interface is used to store and retrieve event-sourced aggregates with correlation information. It is typically implemented by the `CorrelatedStreamStoreRepository` class.

```csharp
// Create a repository
var streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder();
var eventStoreConnection = new StreamStoreConnection("MyApp", connectionSettings, "localhost", 1113);
var serializer = new JsonMessageSerializer();
var repository = new StreamStoreRepository(streamNameBuilder, eventStoreConnection, serializer);
var correlatedRepository = new CorrelatedStreamStoreRepository(repository);

// Create a command with correlation information
ICorrelatedMessage command = MessageBuilder.New(() => new CreateAccount(Guid.NewGuid()));

// Create a new aggregate with correlation information
var account = new Account(Guid.NewGuid(), command);
account.Deposit(100);

// Save the aggregate
correlatedRepository.Save(account);

// Retrieve the aggregate with correlation information
var retrievedAccount = correlatedRepository.GetById<Account>(account.Id, command);

// Update the aggregate
retrievedAccount.Withdraw(50);
correlatedRepository.Save(retrievedAccount);

// Delete the aggregate
correlatedRepository.Delete(retrievedAccount);
```

## Correlation and Causation

The `ICorrelatedRepository` interface helps track correlation and causation IDs across message flows:

- **Correlation ID**: Identifies a business transaction that spans multiple messages
- **Causation ID**: Identifies the message that caused the current message

When an aggregate is loaded with a source message, the source message's correlation and causation IDs are propagated to any events raised by the aggregate. This allows tracking the flow of messages through the system.

## Related Types

- [IRepository](irepository.md): The base repository interface
- [IEventSource](ievent-source.md): The interface for event-sourced entities
- [AggregateRoot](aggregate-root.md): Base class for domain aggregates
- [ICorrelatedMessage](icorrelated-message.md): Interface for correlated messages
- [ICorrelatedEventSource](icorrelated-event-source.md): Interface for correlation tracking in event sources
- [CorrelatedStreamStoreRepository](correlated-stream-store-repository.md): Implementation of `ICorrelatedRepository`

[↑ Back to Top](#icorrelatedrepository-interface) | [← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)
