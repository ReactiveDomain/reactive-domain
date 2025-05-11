# IReadModelRepository

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

`IReadModelRepository` is a core interface in Reactive Domain that defines the contract for repositories that store and retrieve read models.

## Overview

In a CQRS (Command Query Responsibility Segregation) architecture, read models are optimized data structures designed specifically for querying. The `IReadModelRepository` interface provides a standard way to interact with these read models, abstracting the underlying storage mechanism and providing a consistent API for read model persistence.

Read model repositories are typically used by event handlers to persist updated read models after processing domain events. They are also used by query services to retrieve read models when responding to queries from clients.

## Interface Definition

```csharp
public interface IReadModelRepository<T> where T : ReadModelBase
{
    T GetById(Guid id);
    IEnumerable<T> GetAll();
    void Save(T item);
    void Delete(Guid id);
}
```

## Key Features

- **Type Safety**: Provides type-safe operations for specific read model types
- **Storage Abstraction**: Abstracts the underlying storage mechanism (in-memory, relational database, document database, etc.)
- **CRUD Operations**: Supports basic Create, Read, Update, and Delete operations
- **Consistency**: Ensures consistent access patterns across different read model types
- **Flexibility**: Can be implemented for various storage technologies based on query requirements

## Usage

### Basic Repository Operations

Here's a simple example of using a read model repository:

```csharp
public class AccountQueryService
{
    private readonly IReadModelRepository<AccountSummary> _repository;
    
    public AccountQueryService(IReadModelRepository<AccountSummary> repository)
    {
        _repository = repository;
    }
    
    public AccountSummary GetAccountById(Guid accountId)
    {
        return _repository.GetById(accountId);
    }
    
    public IEnumerable<AccountSummary> GetAllAccounts()
    {
        return _repository.GetAll();
    }
    
    public void DeleteAccount(Guid accountId)
    {
        _repository.Delete(accountId);
    }
}
```

### In Event Handlers

Read model repositories are commonly used in event handlers to update read models in response to domain events:

```csharp
public class AccountEventHandler : 
    IEventHandler<AccountCreated>,
    IEventHandler<FundsDeposited>
{
    private readonly IReadModelRepository<AccountSummary> _repository;
    
    public AccountEventHandler(IReadModelRepository<AccountSummary> repository)
    {
        _repository = repository;
    }
    
    public void Handle(AccountCreated @event)
    {
        var accountSummary = new AccountSummary(@event.AccountId);
        accountSummary.Update(@event.AccountNumber, @event.CustomerName, @event.InitialBalance);
        _repository.Save(accountSummary);
    }
    
    public void Handle(FundsDeposited @event)
    {
        var accountSummary = _repository.GetById(@event.AccountId);
        if (accountSummary != null)
        {
            accountSummary.Update(
                accountSummary.AccountNumber,
                accountSummary.CustomerName,
                accountSummary.Balance + @event.Amount);
            _repository.Save(accountSummary);
        }
    }
}
```

## Implementation Examples

### In-Memory Repository

A simple in-memory implementation for development or testing:

```csharp
public class InMemoryReadModelRepository<T> : IReadModelRepository<T> where T : ReadModelBase
{
    private readonly Dictionary<Guid, T> _items = new Dictionary<Guid, T>();
    
    public T GetById(Guid id)
    {
        return _items.TryGetValue(id, out var item) ? item : null;
    }
    
    public IEnumerable<T> GetAll()
    {
        return _items.Values;
    }
    
    public void Save(T item)
    {
        _items[item.Id] = item;
    }
    
    public void Delete(Guid id)
    {
        _items.Remove(id);
    }
}
```

### SQL Database Repository

A repository implementation using a SQL database:

```csharp
public class SqlReadModelRepository<T> : IReadModelRepository<T> where T : ReadModelBase
{
    private readonly string _connectionString;
    private readonly string _tableName;
    
    public SqlReadModelRepository(string connectionString, string tableName)
    {
        _connectionString = connectionString;
        _tableName = tableName;
    }
    
    public T GetById(Guid id)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            connection.Open();
            var sql = $"SELECT * FROM {_tableName} WHERE Id = @Id";
            return connection.QuerySingleOrDefault<T>(sql, new { Id = id });
        }
    }
    
    public IEnumerable<T> GetAll()
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            connection.Open();
            var sql = $"SELECT * FROM {_tableName}";
            return connection.Query<T>(sql);
        }
    }
    
    public void Save(T item)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            connection.Open();
            
            // Check if the item exists
            var exists = connection.ExecuteScalar<bool>(
                $"SELECT COUNT(1) FROM {_tableName} WHERE Id = @Id", 
                new { Id = item.Id });
            
            if (exists)
            {
                // Update existing item (simplified example)
                connection.Execute(
                    $"UPDATE {_tableName} SET Data = @Data WHERE Id = @Id", 
                    new { Id = item.Id, Data = JsonConvert.SerializeObject(item) });
            }
            else
            {
                // Insert new item (simplified example)
                connection.Execute(
                    $"INSERT INTO {_tableName} (Id, Data) VALUES (@Id, @Data)", 
                    new { Id = item.Id, Data = JsonConvert.SerializeObject(item) });
            }
        }
    }
    
    public void Delete(Guid id)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            connection.Open();
            connection.Execute($"DELETE FROM {_tableName} WHERE Id = @Id", new { Id = id });
        }
    }
}
```

### Document Database Repository

A repository implementation using a document database:

```csharp
public class DocumentDbReadModelRepository<T> : IReadModelRepository<T> where T : ReadModelBase
{
    private readonly IMongoCollection<T> _collection;
    
    public DocumentDbReadModelRepository(IMongoDatabase database, string collectionName)
    {
        _collection = database.GetCollection<T>(collectionName);
    }
    
    public T GetById(Guid id)
    {
        return _collection.Find(x => x.Id == id).FirstOrDefault();
    }
    
    public IEnumerable<T> GetAll()
    {
        return _collection.Find(_ => true).ToEnumerable();
    }
    
    public void Save(T item)
    {
        _collection.ReplaceOne(
            x => x.Id == item.Id,
            item,
            new ReplaceOptions { IsUpsert = true });
    }
    
    public void Delete(Guid id)
    {
        _collection.DeleteOne(x => x.Id == id);
    }
}
```

## Best Practices

1. **Repository Per Read Model**: Create a separate repository for each read model type
2. **Caching Strategy**: Implement appropriate caching to improve query performance
3. **Optimistic Concurrency**: Consider using optimistic concurrency for read models that might be updated concurrently
4. **Indexing**: Ensure appropriate database indexes for efficient querying
5. **Bulk Operations**: Support bulk operations for better performance when processing multiple items
6. **Transactions**: Use transactions when updating multiple read models to maintain consistency
7. **Error Handling**: Implement proper error handling and retry logic for database operations
8. **Logging**: Include logging for troubleshooting and performance monitoring
9. **Testing**: Create mock implementations for testing event handlers and query services
10. **Storage Selection**: Choose the appropriate storage technology based on query patterns and requirements

## Common Pitfalls

1. **N+1 Query Problem**: Avoid making multiple database queries when a single query would suffice
2. **Over-normalization**: Read models should be denormalized for query efficiency
3. **Ignoring Indexes**: Missing indexes can lead to poor query performance
4. **Tight Coupling**: Avoid coupling read models to specific storage technologies
5. **Synchronous I/O**: Be cautious about making synchronous database calls in high-throughput systems
6. **Missing Error Handling**: Failing to handle database errors can lead to inconsistent read models
7. **Ignoring Eventual Consistency**: Remember that read models may be eventually consistent with the write model

## Related Components

- [ReadModelBase](./read-model-base.md): Base class for read models stored in the repository
- [IEventHandler](./ievent-handler.md): Interface for event handlers that update read models
- [Event](./event.md): Base class for domain events that trigger read model updates
- [AggregateRoot](./aggregate-root.md): Domain entities that raise events processed by event handlers
- [ICorrelatedMessage](./icorrelated-message.md): Interface for tracking message correlation

---

**Navigation**:
- [← Previous: ReadModelBase](./read-model-base.md)
- [↑ Back to Top](#ireadmodelrepository)
- [→ Next: IRepository](./irepository.md)
