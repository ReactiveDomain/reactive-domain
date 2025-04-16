# IRepository Interface

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

## Overview

The `IRepository` interface defines the contract for repositories that store and retrieve event-sourced aggregates in Reactive Domain.

**Namespace**: `ReactiveDomain.Foundation`  
**Assembly**: `ReactiveDomain.Foundation.dll`

```csharp
public interface IRepository
{
    bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate, int version = int.MaxValue) where TAggregate : class, IEventSource;
    TAggregate GetById<TAggregate>(Guid id, int version = int.MaxValue) where TAggregate : class, IEventSource;
    void Update<TAggregate>(ref TAggregate aggregate, int version = int.MaxValue) where TAggregate : class, IEventSource;
    void Save(IEventSource aggregate);
    void Delete(IEventSource aggregate);
    void HardDelete(IEventSource aggregate);
}
```

## Methods

### TryGetById<TAggregate>

Attempts to retrieve an aggregate by its ID.

```csharp
bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate, int version = int.MaxValue) where TAggregate : class, IEventSource;
```

**Type Parameters**:
- `TAggregate`: The type of the aggregate to retrieve.

**Parameters**:
- `id` (`System.Guid`): The ID of the aggregate to retrieve.
- `aggregate` (`TAggregate`): When this method returns, contains the aggregate with the specified ID, if found; otherwise, the default value for the type of the `aggregate` parameter.
- `version` (`System.Int32`, optional): The version of the aggregate to retrieve. Defaults to `int.MaxValue`, which retrieves the latest version.

**Returns**: `System.Boolean` - `true` if the aggregate was found; otherwise, `false`.

**Remarks**: This method attempts to retrieve an aggregate by its ID. If the aggregate is not found, it returns `false` and sets `aggregate` to `null`.

### GetById<TAggregate>

Retrieves an aggregate by its ID.

```csharp
TAggregate GetById<TAggregate>(Guid id, int version = int.MaxValue) where TAggregate : class, IEventSource;
```

**Type Parameters**:
- `TAggregate`: The type of the aggregate to retrieve.

**Parameters**:
- `id` (`System.Guid`): The ID of the aggregate to retrieve.
- `version` (`System.Int32`, optional): The version of the aggregate to retrieve. Defaults to `int.MaxValue`, which retrieves the latest version.

**Returns**: `TAggregate` - The aggregate with the specified ID.

**Exceptions**:
- `ReactiveDomain.AggregateNotFoundException`: Thrown when the aggregate with the specified ID is not found.
- `ReactiveDomain.AggregateDeletedException`: Thrown when the aggregate with the specified ID has been deleted.

**Remarks**: This method retrieves an aggregate by its ID. If the aggregate is not found, it throws an exception.

### Update<TAggregate>

Updates an aggregate with events from the repository.

```csharp
void Update<TAggregate>(ref TAggregate aggregate, int version = int.MaxValue) where TAggregate : class, IEventSource;
```

**Type Parameters**:
- `TAggregate`: The type of the aggregate to update.

**Parameters**:
- `aggregate` (`TAggregate`): The aggregate to update.
- `version` (`System.Int32`, optional): The version to update the aggregate to. Defaults to `int.MaxValue`, which updates to the latest version.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `aggregate` is `null`.
- `System.InvalidOperationException`: Thrown when the version is less than or equal to 0.
- `ReactiveDomain.AggregateNotFoundException`: Thrown when the aggregate with the specified ID is not found.
- `ReactiveDomain.AggregateDeletedException`: Thrown when the aggregate with the specified ID has been deleted.
- `ReactiveDomain.AggregateVersionException`: Thrown when the specified version does not match the expected version.

**Remarks**: This method updates an aggregate with events from the repository. It loads events from the repository and applies them to the aggregate.

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

The `IRepository` interface is used to store and retrieve event-sourced aggregates. It is typically implemented by the `StreamStoreRepository` class, which stores events in an event store.

```csharp
// Create a repository
var streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder();
var eventStoreConnection = new StreamStoreConnection("MyApp", connectionSettings, "localhost", 1113);
var serializer = new JsonMessageSerializer();
var repository = new StreamStoreRepository(streamNameBuilder, eventStoreConnection, serializer);

// Create a new aggregate
var account = new Account(Guid.NewGuid());
account.Deposit(100);

// Save the aggregate
repository.Save(account);

// Retrieve the aggregate
var retrievedAccount = repository.GetById<Account>(account.Id);

// Update the aggregate
retrievedAccount.Withdraw(50);
repository.Save(retrievedAccount);

// Delete the aggregate
repository.Delete(retrievedAccount);
```

## Related Types

- [IEventSource](ievent-source.md): The interface for event-sourced entities
- [AggregateRoot](aggregate-root.md): Base class for domain aggregates
- [StreamStoreRepository](stream-store-repository.md): Implementation of `IRepository`
- [ICorrelatedRepository](icorrelated-repository.md): Repository with correlation support

[↑ Back to Top](#irepository-interface) | [← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)
