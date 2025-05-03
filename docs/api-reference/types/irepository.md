# IRepository Interface

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

## Overview

The `IRepository` interface defines the contract for repositories that store and retrieve event-sourced aggregates in Reactive Domain. It is a fundamental component in the event sourcing pattern, serving as the bridge between domain aggregates and the underlying event store.

Repositories in Reactive Domain follow the Repository pattern from Domain-Driven Design (DDD), providing a collection-like interface to access domain aggregates while abstracting away the details of event storage and retrieval. The `IRepository` interface ensures that all implementations provide consistent behavior for storing, retrieving, and managing aggregates.

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

Attempts to retrieve an aggregate by its ID. This method provides a non-throwing alternative to `GetById` when you need to check for the existence of an aggregate without handling exceptions.

```csharp
bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate, int version = int.MaxValue) where TAggregate : class, IEventSource;
```

**Type Parameters**:
- `TAggregate`: The type of the aggregate to retrieve. Must be a class that implements `IEventSource`.

**Parameters**:
- `id` (`System.Guid`): The ID of the aggregate to retrieve.
- `aggregate` (`TAggregate`): When this method returns, contains the aggregate with the specified ID, if found; otherwise, the default value for the type of the `aggregate` parameter.
- `version` (`System.Int32`, optional): The version of the aggregate to retrieve. Defaults to `int.MaxValue`, which retrieves the latest version.

**Returns**: `System.Boolean` - `true` if the aggregate was found; otherwise, `false`.

**Example**:
```csharp
// Try to retrieve an account by ID
Account account;
if (repository.TryGetById(accountId, out account))
{
    // Account exists, proceed with operations
    decimal balance = account.GetBalance();
    Console.WriteLine($"Account found with balance: {balance}");
}
else
{
    // Account doesn't exist, handle accordingly
    Console.WriteLine("Account not found");
}
```

**Remarks**: This method attempts to retrieve an aggregate by its ID. If the aggregate is not found, it returns `false` and sets `aggregate` to `null`. This is useful when you want to check if an aggregate exists without throwing exceptions.

### GetById<TAggregate>

Retrieves an aggregate by its ID. This is the primary method for loading aggregates from the repository.

```csharp
TAggregate GetById<TAggregate>(Guid id, int version = int.MaxValue) where TAggregate : class, IEventSource;
```

**Type Parameters**:
- `TAggregate`: The type of the aggregate to retrieve. Must be a class that implements `IEventSource`.

**Parameters**:
- `id` (`System.Guid`): The ID of the aggregate to retrieve.
- `version` (`System.Int32`, optional): The version of the aggregate to retrieve. Defaults to `int.MaxValue`, which retrieves the latest version.

**Returns**: `TAggregate` - The aggregate with the specified ID.

**Exceptions**:
- `ReactiveDomain.AggregateNotFoundException`: Thrown when the aggregate with the specified ID is not found.
- `ReactiveDomain.AggregateDeletedException`: Thrown when the aggregate with the specified ID has been deleted.

**Example**:
```csharp
// Retrieve an account by ID
try
{
    var account = repository.GetById<Account>(accountId);
    
    // Perform operations on the account
    decimal balance = account.GetBalance();
    Console.WriteLine($"Account balance: {balance}");
    
    if (balance > 0)
    {
        account.Withdraw(balance);
        repository.Save(account);
    }
}
catch (AggregateNotFoundException)
{
    Console.WriteLine("Account not found");
}
catch (AggregateDeletedException)
{
    Console.WriteLine("Account has been deleted");
}
```

**Remarks**: This method retrieves an aggregate by its ID. If the aggregate is not found or has been deleted, it throws an exception. Use this method when you expect the aggregate to exist and want to handle exceptions for specific error cases.

### Update<TAggregate>

Updates an aggregate with events from the repository. This method is used to refresh an aggregate with the latest events from the event store, which is useful in scenarios where the aggregate might have been modified by another process.

```csharp
void Update<TAggregate>(ref TAggregate aggregate, int version = int.MaxValue) where TAggregate : class, IEventSource;
```

**Type Parameters**:
- `TAggregate`: The type of the aggregate to update. Must be a class that implements `IEventSource`.

**Parameters**:
- `aggregate` (`TAggregate`): The aggregate to update. This parameter is passed by reference and will be updated with the latest events.
- `version` (`System.Int32`, optional): The version to update the aggregate to. Defaults to `int.MaxValue`, which updates to the latest version.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `aggregate` is `null`.
- `System.InvalidOperationException`: Thrown when the version is less than or equal to 0.
- `ReactiveDomain.AggregateNotFoundException`: Thrown when the aggregate with the specified ID is not found.
- `ReactiveDomain.AggregateDeletedException`: Thrown when the aggregate with the specified ID has been deleted.
- `ReactiveDomain.AggregateVersionException`: Thrown when the specified version does not match the expected version.

**Example**:
```csharp
// Retrieve an account
var account = repository.GetById<Account>(accountId);

// Perform some long-running operation
PerformLongRunningOperation();

// Update the account with the latest events before proceeding
// This ensures we have the most up-to-date state
try
{
    repository.Update(ref account);
    
    // Now we can safely perform operations on the updated account
    if (account.GetBalance() >= amount)
    {
        account.Withdraw(amount);
        repository.Save(account);
    }
}
catch (AggregateVersionException)
{
    Console.WriteLine("The account was modified concurrently");
}
```

**Remarks**: This method updates an aggregate with events from the repository. It loads events from the repository and applies them to the aggregate. This is useful when you need to ensure that you're working with the most up-to-date state of an aggregate before performing operations on it.

### Save

Saves an aggregate to the repository. This method persists the new events generated by the aggregate to the event store.

```csharp
void Save(IEventSource aggregate);
```

**Parameters**:
- `aggregate` (`ReactiveDomain.IEventSource`): The aggregate to save.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `aggregate` is `null`.
- `ReactiveDomain.AggregateVersionException`: Thrown when the aggregate's expected version does not match the version in the repository.

**Example**:
```csharp
// Create a new account
var account = new Account(Guid.NewGuid());

// Perform operations on the account
account.Deposit(1000);
account.Withdraw(500);

// Save the account to the repository
try
{
    repository.Save(account);
    Console.WriteLine("Account saved successfully");
}
catch (AggregateVersionException)
{
    Console.WriteLine("Concurrent modification detected");
    // Handle the concurrency conflict
}
```

**Remarks**: This method saves an aggregate to the repository. It takes the events from the aggregate and appends them to the event stream in the repository. If the aggregate's expected version does not match the version in the repository, it throws an `AggregateVersionException`, indicating a concurrent modification.

### Delete

Marks an aggregate as deleted in the repository. This method does not physically remove the aggregate from the event store but appends a deletion event to its stream.

```csharp
void Delete(IEventSource aggregate);
```

**Parameters**:
- `aggregate` (`ReactiveDomain.IEventSource`): The aggregate to delete.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `aggregate` is `null`.
- `ReactiveDomain.AggregateVersionException`: Thrown when the aggregate's expected version does not match the version in the repository.

**Example**:
```csharp
// Retrieve an account
var account = repository.GetById<Account>(accountId);

// Mark the account as deleted
try
{
    repository.Delete(account);
    Console.WriteLine("Account marked as deleted");
}
catch (AggregateVersionException)
{
    Console.WriteLine("Concurrent modification detected");
    // Handle the concurrency conflict
}

// Attempting to retrieve the account now will throw AggregateDeletedException
try
{
    var deletedAccount = repository.GetById<Account>(accountId);
}
catch (AggregateDeletedException)
{
    Console.WriteLine("Account has been deleted");
}
```

**Remarks**: This method marks an aggregate as deleted in the repository. It appends a deletion event to the event stream. The aggregate can still be retrieved, but will be marked as deleted, and attempts to retrieve it with `GetById` will throw an `AggregateDeletedException`.

### HardDelete

Permanently deletes an aggregate from the repository. This method physically removes the aggregate's event stream from the event store.

```csharp
void HardDelete(IEventSource aggregate);
```

**Parameters**:
- `aggregate` (`ReactiveDomain.IEventSource`): The aggregate to delete.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `aggregate` is `null`.
- `ReactiveDomain.AggregateVersionException`: Thrown when the aggregate's expected version does not match the version in the repository.

**Example**:
```csharp
// Retrieve an account
var account = repository.GetById<Account>(accountId);

// Permanently delete the account
try
{
    repository.HardDelete(account);
    Console.WriteLine("Account permanently deleted");
}
catch (AggregateVersionException)
{
    Console.WriteLine("Concurrent modification detected");
    // Handle the concurrency conflict
}

// Attempting to retrieve the account now will throw AggregateNotFoundException
try
{
    var deletedAccount = repository.GetById<Account>(accountId);
}
catch (AggregateNotFoundException)
{
    Console.WriteLine("Account not found (was permanently deleted)");
}
```

**Remarks**: This method permanently deletes an aggregate from the repository. It removes the event stream from the repository. The aggregate cannot be retrieved after this operation. Use this method with caution, as it permanently removes data from the system.

## Usage

The `IRepository` interface is used to store and retrieve event-sourced aggregates. It is typically implemented by the `StreamStoreRepository` class, which stores events in an event store. Here's a comprehensive example of using a repository in a typical application scenario:

```csharp
// Create a repository
var streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder();
var eventStoreConnection = new StreamStoreConnection("MyApp", connectionSettings, "localhost", 1113);
var serializer = new JsonMessageSerializer();
var repository = new StreamStoreRepository(streamNameBuilder, eventStoreConnection, serializer);

// Create a new aggregate
var accountId = Guid.NewGuid();
var account = new Account(accountId);
account.Deposit(1000);

// Save the aggregate
repository.Save(account);
Console.WriteLine($"Created account {accountId} with initial deposit of $1000");

// Retrieve the aggregate
var retrievedAccount = repository.GetById<Account>(accountId);
Console.WriteLine($"Retrieved account balance: ${retrievedAccount.GetBalance()}");

// Perform operations on the aggregate
retrievedAccount.Withdraw(500);
Console.WriteLine($"Withdrew $500, new balance: ${retrievedAccount.GetBalance()}");

// Save the updated aggregate
repository.Save(retrievedAccount);
Console.WriteLine("Saved account after withdrawal");

// Update the aggregate with the latest events
repository.Update(ref retrievedAccount);
Console.WriteLine($"Updated account balance: ${retrievedAccount.GetBalance()}");

// Delete the aggregate (soft delete)
repository.Delete(retrievedAccount);
Console.WriteLine("Deleted account (soft delete)");

// Hard delete the aggregate (permanent deletion)
// repository.HardDelete(retrievedAccount);
// Console.WriteLine("Permanently deleted account");
```

## Best Practices

1. **Optimistic Concurrency**: Always handle `AggregateVersionException` to manage concurrent modifications
2. **Aggregate Lifecycle**: Use `Delete` for logical deletion and `HardDelete` only when data must be permanently removed
3. **Version Management**: Use the `version` parameter in `GetById` and `Update` to work with specific versions of aggregates
4. **Error Handling**: Implement proper exception handling for repository operations
5. **Transaction Boundaries**: Consider repository operations as transaction boundaries in your domain
6. **Repository Abstraction**: Depend on the `IRepository` interface rather than concrete implementations
7. **Correlation Tracking**: Use `ICorrelatedRepository` when correlation information needs to be maintained

## Common Pitfalls

1. **Ignoring Concurrency**: Failing to handle `AggregateVersionException` can lead to lost updates
2. **Large Aggregates**: Storing too many events in a single aggregate can impact performance
3. **Missing Version Checks**: Not checking versions when updating aggregates can lead to inconsistent state
4. **Hard Deletion Overuse**: Using `HardDelete` when `Delete` would be more appropriate
5. **Repository Leakage**: Allowing repository implementation details to leak into the domain model
6. **Missing Error Handling**: Not properly handling repository exceptions

## Related Types

- [IEventSource](ievent-source.md): The interface for event-sourced entities
- [AggregateRoot](aggregate-root.md): Base class for domain aggregates
- [StreamStoreRepository](stream-store-repository.md): Implementation of `IRepository`
- [ICorrelatedRepository](icorrelated-repository.md): Repository with correlation support

[↑ Back to Top](#irepository-interface) | [← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)
