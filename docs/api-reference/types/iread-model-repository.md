# IReadModelRepository

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

`IReadModelRepository` is a core interface in Reactive Domain that defines the contract for repositories that store and retrieve read models.

## Overview

In a CQRS (Command Query Responsibility Segregation) architecture, read models are optimized data structures designed specifically for querying. The `IReadModelRepository` interface provides a standard way to interact with these read models, abstracting the underlying storage mechanism and providing a consistent API for read model persistence.

Read model repositories are typically used by event handlers to persist updated read models after processing domain events. They are also used by query services to retrieve read models when responding to queries from clients.

## Interface Definition

### Basic Interface

```csharp
public interface IReadModelRepository<T> where T : ReadModelBase
{
    T GetById(Guid id);
    IEnumerable<T> GetAll();
    void Save(T item);
    void Delete(Guid id);
}
```

### Extended Interface with Async Support

For modern applications, an asynchronous version is recommended:

```csharp
public interface IAsyncReadModelRepository<T> where T : ReadModelBase
{
    Task<T> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(T item, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    
    // Optional: Batch operations for performance
    Task SaveManyAsync(IEnumerable<T> items, CancellationToken cancellationToken = default);
    Task DeleteManyAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
}
```

### Query-Enhanced Interface

For more complex query scenarios:

```csharp
public interface IQueryableReadModelRepository<T> where T : ReadModelBase
{
    // Basic operations
    Task<T> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(T item, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    
    // Advanced query capabilities
    Task<IEnumerable<T>> FindAsync(
        Expression<Func<T, bool>> predicate, 
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<T>> FindAsync(
        Expression<Func<T, bool>> predicate, 
        int skip, 
        int take, 
        CancellationToken cancellationToken = default);
    
    Task<int> CountAsync(
        Expression<Func<T, bool>> predicate, 
        CancellationToken cancellationToken = default);
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

### Modern In-Memory Repository with Async Support

A thread-safe in-memory implementation with async support for development or testing:

```csharp
public class InMemoryReadModelRepository<T> : IAsyncReadModelRepository<T> where T : ReadModelBase
{
    private readonly ConcurrentDictionary<Guid, T> _items = new ConcurrentDictionary<Guid, T>();
    private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
    
    public async Task<T> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // For consistent async behavior
        return _items.TryGetValue(id, out var item) ? item : null;
    }
    
    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // For consistent async behavior
        return _items.Values.ToList(); // Return a copy to avoid modification issues
    }
    
    public async Task SaveAsync(T item, CancellationToken cancellationToken = default)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _items[item.Id] = item;
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task SaveManyAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        
        await _lock.WaitAsync(cancellationToken);
        try
        {
            foreach (var item in items)
            {
                _items[item.Id] = item;
            }
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _items.TryRemove(id, out _);
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task DeleteManyAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids == null) throw new ArgumentNullException(nameof(ids));
        
        await _lock.WaitAsync(cancellationToken);
        try
        {
            foreach (var id in ids)
            {
                _items.TryRemove(id, out _);
            }
        }
        finally
        {
            _lock.Release();
        }
    }
}
```

### Entity Framework Core Repository

A modern repository implementation using Entity Framework Core:

```csharp
public class EfCoreReadModelRepository<T> : IQueryableReadModelRepository<T> 
    where T : ReadModelBase
{
    private readonly ReadModelDbContext _dbContext;
    private readonly ILogger<EfCoreReadModelRepository<T>> _logger;
    
    public EfCoreReadModelRepository(
        ReadModelDbContext dbContext,
        ILogger<EfCoreReadModelRepository<T>> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }
    
    public async Task<T> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.Set<T>()
                .AsNoTracking() // For read-only operations
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving read model {ReadModelType} with ID {Id}", 
                typeof(T).Name, id);
            throw;
        }
    }
    
    public async Task<IEnumerable<T>> FindAsync(
        Expression<Func<T, bool>> predicate, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.Set<T>()
                .AsNoTracking()
                .Where(predicate)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding read models of type {ReadModelType}", 
                typeof(T).Name);
            throw;
        }
    }
    
    public async Task<IEnumerable<T>> FindAsync(
        Expression<Func<T, bool>> predicate, 
        int skip, 
        int take, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.Set<T>()
                .AsNoTracking()
                .Where(predicate)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding paged read models of type {ReadModelType}", 
                typeof(T).Name);
            throw;
        }
    }
    
    public async Task<int> CountAsync(
        Expression<Func<T, bool>> predicate, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.Set<T>()
                .Where(predicate)
                .CountAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting read models of type {ReadModelType}", 
                typeof(T).Name);
            throw;
        }
    }
    
    public async Task SaveAsync(T item, CancellationToken cancellationToken = default)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        
        try
        {
            // Use a clean approach to avoid tracking issues
            var existingEntity = await _dbContext.Set<T>().FindAsync(new object[] { item.Id }, cancellationToken);
            
            if (existingEntity != null)
            {
                // Detach existing entity to avoid tracking conflicts
                _dbContext.Entry(existingEntity).State = EntityState.Detached;
            }
            
            // Attach and mark as modified (or added if new)
            _dbContext.Entry(item).State = existingEntity != null ? 
                EntityState.Modified : 
                EntityState.Added;
                
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            // Detach the entity after saving to avoid tracking issues in future operations
            _dbContext.Entry(item).State = EntityState.Detached;
            
            _logger.LogDebug(
                "{Operation} read model {ReadModelType} with ID {Id}",
                existingEntity != null ? "Updated" : "Created",
                typeof(T).Name,
                item.Id);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, 
                "Concurrency conflict when saving read model {ReadModelType} with ID {Id}", 
                typeof(T).Name, item.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error saving read model {ReadModelType} with ID {Id}", 
                typeof(T).Name, item.Id);
            throw;
        }
    }
    
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _dbContext.Set<T>().FindAsync(new object[] { id }, cancellationToken);
            if (entity != null)
            {
                _dbContext.Set<T>().Remove(entity);
                await _dbContext.SaveChangesAsync(cancellationToken);
                
                _logger.LogDebug(
                    "Deleted read model {ReadModelType} with ID {Id}",
                    typeof(T).Name,
                    id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error deleting read model {ReadModelType} with ID {Id}", 
                typeof(T).Name, id);
            throw;
        }
    }
}
```

### MongoDB Repository with Async Support

A modern repository implementation using MongoDB with async operations:

```csharp
public class MongoDbReadModelRepository<T> : IAsyncReadModelRepository<T> 
    where T : ReadModelBase
{
    private readonly IMongoCollection<T> _collection;
    private readonly ILogger<MongoDbReadModelRepository<T>> _logger;
    
    public MongoDbReadModelRepository(
        IMongoDatabase database, 
        string collectionName,
        ILogger<MongoDbReadModelRepository<T>> logger)
    {
        _collection = database.GetCollection<T>(collectionName);
        _logger = logger;
        
        // Ensure we have an index on the Id field
        var indexKeysDefinition = Builders<T>.IndexKeys.Ascending(x => x.Id);
        _collection.Indexes.CreateOne(new CreateIndexModel<T>(indexKeysDefinition));
    }
    
    public async Task<T> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _collection
                .Find(x => x.Id == id)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error retrieving read model {ReadModelType} with ID {Id}", 
                typeof(T).Name, id);
            throw;
        }
    }
    
    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _collection
                .Find(_ => true)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error retrieving all read models of type {ReadModelType}", 
                typeof(T).Name);
            throw;
        }
    }
    
    public async Task SaveAsync(T item, CancellationToken cancellationToken = default)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        
        try
        {
            await _collection.ReplaceOneAsync(
                x => x.Id == item.Id,
                item,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);
                
            _logger.LogDebug(
                "Saved read model {ReadModelType} with ID {Id}",
                typeof(T).Name,
                item.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error saving read model {ReadModelType} with ID {Id}", 
                typeof(T).Name, item.Id);
            throw;
        }
    }
    
    public async Task SaveManyAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        var itemsList = items.ToList();
        if (!itemsList.Any()) return;
        
        try
        {
            var bulkOps = new List<WriteModel<T>>();
            
            foreach (var item in itemsList)
            {
                var filter = Builders<T>.Filter.Eq(x => x.Id, item.Id);
                var replaceModel = new ReplaceOneModel<T>(filter, item) { IsUpsert = true };
                bulkOps.Add(replaceModel);
            }
            
            await _collection.BulkWriteAsync(bulkOps, cancellationToken: cancellationToken);
            
            _logger.LogDebug(
                "Saved {Count} read models of type {ReadModelType}",
                itemsList.Count,
                typeof(T).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error saving multiple read models of type {ReadModelType}", 
                typeof(T).Name);
            throw;
        }
    }
    
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _collection.DeleteOneAsync(x => x.Id == id, cancellationToken);
            
            _logger.LogDebug(
                "Deleted read model {ReadModelType} with ID {Id}",
                typeof(T).Name,
                id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error deleting read model {ReadModelType} with ID {Id}", 
                typeof(T).Name, id);
            throw;
        }
    }
    
    public async Task DeleteManyAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids == null) throw new ArgumentNullException(nameof(ids));
        var idsList = ids.ToList();
        if (!idsList.Any()) return;
        
        try
        {
            var filter = Builders<T>.Filter.In(x => x.Id, idsList);
            await _collection.DeleteManyAsync(filter, cancellationToken);
            
            _logger.LogDebug(
                "Deleted {Count} read models of type {ReadModelType}",
                idsList.Count,
                typeof(T).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error deleting multiple read models of type {ReadModelType}", 
                typeof(T).Name);
            throw;
        }
    }
}
```

## Best Practices

### Architecture and Design

1. **Repository Per Read Model**: Create a separate repository for each read model type to maintain a clean separation of concerns and avoid mixing query patterns that might be optimized differently.

   ```csharp
   // Good: Specific repositories for specific read models
   public interface IAccountSummaryRepository : IAsyncReadModelRepository<AccountSummary> { }
   public interface ITransactionHistoryRepository : IAsyncReadModelRepository<TransactionHistory> { }
   
   // Implementation
   public class AccountSummaryRepository : MongoDbReadModelRepository<AccountSummary>, IAccountSummaryRepository
   {
       public AccountSummaryRepository(IMongoDatabase database, ILogger<AccountSummaryRepository> logger)
           : base(database, "account_summaries", logger) { }
       
       // Additional account-specific query methods can be added here
   }
   ```

2. **Storage Technology Selection**: Choose the storage technology based on query patterns rather than write patterns:
   - Use document databases (MongoDB, CosmosDB) for hierarchical data or when schema flexibility is needed
   - Use relational databases for complex queries with joins or when ACID compliance is required
   - Use search engines (Elasticsearch) for full-text search and complex filtering
   - Use in-memory databases or caches for high-performance needs with small datasets

3. **Interface Segregation**: Define specialized repository interfaces for specific query needs rather than creating a one-size-fits-all interface.

   ```csharp
   // Good: Specialized interface for specific query needs
   public interface ICustomerDashboardRepository : IAsyncReadModelRepository<CustomerDashboard>
   {
       Task<IEnumerable<CustomerDashboard>> FindByActivityDateAsync(
           DateTime startDate, 
           DateTime endDate, 
           CancellationToken cancellationToken = default);
       
       Task<IEnumerable<CustomerDashboard>> FindTopCustomersByBalanceAsync(
           int count, 
           CancellationToken cancellationToken = default);
   }
   ```

4. **Dependency Injection**: Register repositories with appropriate lifetimes in your dependency injection container.

   ```csharp
   // In your DI configuration
   services.AddScoped<IAccountSummaryRepository, AccountSummaryRepository>();
   services.AddScoped<ITransactionHistoryRepository, TransactionHistoryRepository>();
   ```

### Performance Optimization

5. **Caching Strategy**: Implement appropriate caching to improve query performance, especially for frequently accessed read models.

   ```csharp
   public class CachedReadModelRepository<T> : IAsyncReadModelRepository<T> where T : ReadModelBase
   {
       private readonly IAsyncReadModelRepository<T> _repository;
       private readonly IMemoryCache _cache;
       private readonly TimeSpan _cacheDuration;
       
       public CachedReadModelRepository(
           IAsyncReadModelRepository<T> repository,
           IMemoryCache cache,
           TimeSpan cacheDuration)
       {
           _repository = repository;
           _cache = cache;
           _cacheDuration = cacheDuration;
       }
       
       public async Task<T> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
       {
           var cacheKey = $"{typeof(T).Name}:{id}";
           
           if (!_cache.TryGetValue(cacheKey, out T item))
           {
               item = await _repository.GetByIdAsync(id, cancellationToken);
               
               if (item != null)
               {
                   _cache.Set(cacheKey, item, _cacheDuration);
               }
           }
           
           return item;
       }
       
       // Other methods with appropriate caching...
   }
   ```

6. **Indexing**: Ensure appropriate database indexes for efficient querying, especially for fields used in filtering, sorting, or joining.

7. **Bulk Operations**: Support bulk operations for better performance when processing multiple items, especially during event replay or system initialization.

8. **Pagination**: Implement pagination for large result sets to avoid memory issues and improve response times.

   ```csharp
   public class PagedResult<T>
   {
       public IEnumerable<T> Items { get; }
       public int TotalCount { get; }
       public int PageNumber { get; }
       public int PageSize { get; }
       public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
       
       public PagedResult(IEnumerable<T> items, int totalCount, int pageNumber, int pageSize)
       {
           Items = items;
           TotalCount = totalCount;
           PageNumber = pageNumber;
           PageSize = pageSize;
       }
   }
   
   // In your repository interface
   Task<PagedResult<T>> GetPagedAsync(
       Expression<Func<T, bool>> filter, 
       int pageNumber, 
       int pageSize, 
       CancellationToken cancellationToken = default);
   ```

### Robustness and Maintainability

9. **Error Handling and Resilience**: Implement proper error handling, retry logic, and circuit breakers for database operations.

   ```csharp
   // Using Polly for resilience
   var retryPolicy = Policy
       .Handle<MongoConnectionException>()
       .Or<MongoCommandException>()
       .WaitAndRetryAsync(
           retryCount: 3,
           sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
           onRetry: (exception, timeSpan, retryCount, context) =>
           {
               _logger.LogWarning(
                   exception,
                   "Retry {RetryCount} after {RetryDelay}ms due to {ExceptionType}",
                   retryCount,
                   timeSpan.TotalMilliseconds,
                   exception.GetType().Name);
           });
   
   // Usage in repository method
   public async Task<T> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
   {
       return await retryPolicy.ExecuteAsync(() => 
           _collection.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken));
   }
   ```

10. **Comprehensive Logging**: Include detailed logging for troubleshooting, performance monitoring, and auditing.

11. **Testing**: Create mock implementations and test fixtures for testing event handlers and query services.

    ```csharp
    public class TestReadModelRepository<T> : IAsyncReadModelRepository<T> where T : ReadModelBase
    {
        private readonly ConcurrentDictionary<Guid, T> _items = new ConcurrentDictionary<Guid, T>();
        
        // Repository methods implementation...
        
        // Test-specific methods
        public void Reset()
        {
            _items.Clear();
        }
        
        public void SetupTestData(IEnumerable<T> testData)
        {
            foreach (var item in testData)
            {
                _items[item.Id] = item;
            }
        }
    }
    ```

12. **Versioning Strategy**: Include support for schema versioning to handle read model evolution over time.

13. **Monitoring and Observability**: Add metrics collection for repository operations to track performance and detect issues.

    ```csharp
    public async Task<T> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("ReadModel.GetById");
        activity?.SetTag("readModel.type", typeof(T).Name);
        activity?.SetTag("readModel.id", id);
        
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await _collection.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);
            
            _metrics.RecordRepositoryOperation(
                "GetById", 
                typeof(T).Name, 
                stopwatch.ElapsedMilliseconds, 
                result != null);
                
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _metrics.RecordRepositoryError("GetById", typeof(T).Name, ex.GetType().Name);
            throw;
        }
    }
    ```

## Common Pitfalls

### Performance Issues

1. **N+1 Query Problem**: Avoid making multiple database queries when a single query would suffice, especially in event handlers processing multiple events.

   ```csharp
   // Bad: N+1 query problem
   foreach (var id in accountIds)
   {
       var account = await _repository.GetByIdAsync(id);
       // Process account...
   }
   
   // Good: Batch query
   var accounts = await _repository.FindAsync(x => accountIds.Contains(x.Id));
   foreach (var account in accounts)
   {
       // Process account...
   }
   ```

2. **Inefficient Queries**: Avoid complex queries against read models. If you need complex queries, consider creating a specialized read model optimized for that query pattern.

3. **Missing Indexes**: Failing to create appropriate indexes can lead to full table scans and poor query performance, especially as your data grows.

4. **Synchronous I/O in High-Throughput Systems**: Using synchronous database calls in high-throughput systems can lead to thread pool starvation and reduced throughput.

   ```csharp
   // Bad: Synchronous I/O
   public void Handle(AccountCreated @event)
   {
       var account = _repository.GetById(@event.AccountId); // Blocks thread
       // Process...
       _repository.Save(account); // Blocks thread
   }
   
   // Good: Asynchronous I/O
   public async Task HandleAsync(AccountCreated @event)
   {
       var account = await _repository.GetByIdAsync(@event.AccountId);
       // Process...
       await _repository.SaveAsync(account);
   }
   ```

5. **Connection Management Issues**: Not properly managing database connections can lead to connection pool exhaustion.

### Design Flaws

6. **Over-normalization**: Read models should be denormalized for query efficiency. Don't try to maintain normal forms as you would in a traditional database design.

7. **Tight Coupling to Storage Technology**: Avoid coupling your domain logic or projections directly to specific storage technologies. Use the repository abstraction to isolate these concerns.

8. **One-Size-Fits-All Repositories**: Trying to create a single repository implementation that works efficiently for all read models and query patterns often leads to suboptimal performance.

9. **Ignoring Eventual Consistency**: Failing to design your system to handle eventual consistency between write and read models can lead to confusing user experiences and bugs.

   ```csharp
   // Bad: Assuming immediate consistency
   public async Task<ActionResult> CreateAccount(CreateAccountCommand command)
   {
       await _commandBus.SendAsync(command);
       // Immediately trying to read the account might fail
       var account = await _accountRepository.GetByIdAsync(command.AccountId);
       return Ok(account); // Might return null if read model not yet updated
   }
   
   // Good: Handling eventual consistency
   public async Task<ActionResult> CreateAccount(CreateAccountCommand command)
   {
       await _commandBus.SendAsync(command);
       // Return the ID and let the client poll or use SignalR for updates
       return Accepted(new { Id = command.AccountId });
   }
   ```

### Operational Challenges

10. **Missing Error Handling**: Failing to handle database errors can lead to inconsistent read models and hard-to-debug issues.

11. **Inadequate Logging**: Without proper logging, it's difficult to troubleshoot issues with read model projections and queries.

12. **No Rebuild Strategy**: Not having a way to rebuild read models from event streams makes it difficult to recover from data corruption or create new read model types.

13. **Ignoring Database-Specific Behaviors**: Each database technology has its own quirks and best practices. Ignoring these can lead to suboptimal performance or unexpected behavior.

14. **Lack of Monitoring**: Without proper monitoring, it's difficult to detect when read models are out of sync with the write model or when projections are failing.

15. **Unbounded Result Sets**: Not implementing pagination or limits on query results can lead to out-of-memory exceptions or poor performance when dealing with large datasets.

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
