# Query Handling

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

Query handling is a critical part of the CQRS (Command Query Responsibility Segregation) pattern in Reactive Domain. This document outlines the best practices and implementation patterns for handling queries efficiently.

## Overview

In CQRS, queries are responsible for retrieving data from the system without causing any state changes. They are typically executed against read models that are optimized for specific query scenarios. This separation allows for:

- Optimizing read and write operations independently
- Scaling read and write sides separately
- Using different storage technologies for reads and writes
- Implementing specialized query patterns without affecting the domain model

## Query Types

### 1. Direct Queries

Direct queries retrieve data based on specific identifiers or simple criteria.

```csharp
// Query definition
public class GetAccountByIdQuery
{
    public Guid AccountId { get; }
    
    public GetAccountByIdQuery(Guid accountId)
    {
        AccountId = accountId;
    }
}

// Query result
public class AccountDto
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; }
    public string CustomerName { get; set; }
    public decimal Balance { get; set; }
    public AccountStatus Status { get; set; }
    public DateTime LastUpdated { get; set; }
}
```

### 2. List Queries

List queries retrieve collections of items, often with filtering, sorting, and pagination.

```csharp
// Query definition
public class ListAccountsQuery
{
    public string CustomerNameFilter { get; }
    public AccountStatus? StatusFilter { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public string SortBy { get; }
    public bool SortDescending { get; }
    
    public ListAccountsQuery(
        string customerNameFilter = null,
        AccountStatus? statusFilter = null,
        int pageNumber = 1,
        int pageSize = 20,
        string sortBy = "LastUpdated",
        bool sortDescending = true)
    {
        CustomerNameFilter = customerNameFilter;
        StatusFilter = statusFilter;
        PageNumber = pageNumber > 0 ? pageNumber : 1;
        PageSize = pageSize > 0 && pageSize <= 100 ? pageSize : 20;
        SortBy = sortBy;
        SortDescending = sortDescending;
    }
}

// Query result
public class AccountListResult
{
    public IEnumerable<AccountDto> Accounts { get; }
    public int TotalCount { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    
    public AccountListResult(
        IEnumerable<AccountDto> accounts,
        int totalCount,
        int pageNumber,
        int pageSize)
    {
        Accounts = accounts;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }
}
```

### 3. Aggregate Queries

Aggregate queries perform calculations or aggregations on data.

```csharp
// Query definition
public class GetAccountBalanceSummaryQuery
{
    public Guid CustomerId { get; }
    
    public GetAccountBalanceSummaryQuery(Guid customerId)
    {
        CustomerId = customerId;
    }
}

// Query result
public class CustomerBalanceSummary
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; }
    public int TotalAccounts { get; set; }
    public decimal TotalBalance { get; set; }
    public decimal HighestAccountBalance { get; set; }
    public decimal AverageAccountBalance { get; set; }
}
```

## Query Handlers

Query handlers are responsible for processing queries and returning results. They typically interact with read model repositories to retrieve data.

### Interface Definition

```csharp
// Synchronous query handler
public interface IQueryHandler<TQuery, TResult>
{
    TResult Handle(TQuery query);
}

// Asynchronous query handler (recommended)
public interface IAsyncQueryHandler<TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
```

### Implementation Example

```csharp
public class GetAccountByIdQueryHandler : IAsyncQueryHandler<GetAccountByIdQuery, AccountDto>
{
    private readonly IAsyncReadModelRepository<AccountSummary> _repository;
    private readonly ILogger<GetAccountByIdQueryHandler> _logger;
    
    public GetAccountByIdQueryHandler(
        IAsyncReadModelRepository<AccountSummary> repository,
        ILogger<GetAccountByIdQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }
    
    public async Task<AccountDto> HandleAsync(
        GetAccountByIdQuery query, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var account = await _repository.GetByIdAsync(query.AccountId, cancellationToken);
            
            if (account == null)
            {
                _logger.LogInformation(
                    "Account with ID {AccountId} not found", 
                    query.AccountId);
                return null;
            }
            
            return new AccountDto
            {
                Id = account.Id,
                AccountNumber = account.AccountNumber,
                CustomerName = account.CustomerName,
                Balance = account.Balance,
                Status = account.Status,
                LastUpdated = account.LastUpdated
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving account with ID {AccountId}",
                query.AccountId);
            throw;
        }
    }
}
```

### List Query Handler Example

```csharp
public class ListAccountsQueryHandler : IAsyncQueryHandler<ListAccountsQuery, AccountListResult>
{
    private readonly IQueryableReadModelRepository<AccountSummary> _repository;
    private readonly ILogger<ListAccountsQueryHandler> _logger;
    
    public ListAccountsQueryHandler(
        IQueryableReadModelRepository<AccountSummary> repository,
        ILogger<ListAccountsQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }
    
    public async Task<AccountListResult> HandleAsync(
        ListAccountsQuery query, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Build the filter expression
            Expression<Func<AccountSummary, bool>> filter = account => true;
            
            if (!string.IsNullOrWhiteSpace(query.CustomerNameFilter))
            {
                filter = filter.AndAlso(account => 
                    account.CustomerName.Contains(query.CustomerNameFilter));
            }
            
            if (query.StatusFilter.HasValue)
            {
                filter = filter.AndAlso(account => 
                    account.Status == query.StatusFilter.Value);
            }
            
            // Get total count for pagination
            var totalCount = await _repository.CountAsync(filter, cancellationToken);
            
            // Calculate skip and take values
            var skip = (query.PageNumber - 1) * query.PageSize;
            var take = query.PageSize;
            
            // Get the paged data
            var accounts = await _repository.FindAsync(
                filter, 
                skip, 
                take, 
                cancellationToken);
            
            // Map to DTOs
            var accountDtos = accounts.Select(account => new AccountDto
            {
                Id = account.Id,
                AccountNumber = account.AccountNumber,
                CustomerName = account.CustomerName,
                Balance = account.Balance,
                Status = account.Status,
                LastUpdated = account.LastUpdated
            });
            
            return new AccountListResult(
                accountDtos,
                totalCount,
                query.PageNumber,
                query.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing accounts");
            throw;
        }
    }
}

// Extension method for combining expressions
public static class ExpressionExtensions
{
    public static Expression<Func<T, bool>> AndAlso<T>(
        this Expression<Func<T, bool>> expr1,
        Expression<Func<T, bool>> expr2)
    {
        var parameter = Expression.Parameter(typeof(T));
        
        var leftVisitor = new ReplaceExpressionVisitor(
            expr1.Parameters[0], parameter);
        var left = leftVisitor.Visit(expr1.Body);
        
        var rightVisitor = new ReplaceExpressionVisitor(
            expr2.Parameters[0], parameter);
        var right = rightVisitor.Visit(expr2.Body);
        
        return Expression.Lambda<Func<T, bool>>(
            Expression.AndAlso(left, right), parameter);
    }
    
    private class ReplaceExpressionVisitor : ExpressionVisitor
    {
        private readonly Expression _oldValue;
        private readonly Expression _newValue;
        
        public ReplaceExpressionVisitor(Expression oldValue, Expression newValue)
        {
            _oldValue = oldValue;
            _newValue = newValue;
        }
        
        public override Expression Visit(Expression node)
        {
            if (node == _oldValue)
                return _newValue;
            return base.Visit(node);
        }
    }
}
```

## Query Dispatcher

A query dispatcher provides a centralized way to send queries to their appropriate handlers.

```csharp
public interface IQueryDispatcher
{
    Task<TResult> DispatchAsync<TQuery, TResult>(
        TQuery query, 
        CancellationToken cancellationToken = default);
}

public class QueryDispatcher : IQueryDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QueryDispatcher> _logger;
    
    public QueryDispatcher(
        IServiceProvider serviceProvider,
        ILogger<QueryDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    public async Task<TResult> DispatchAsync<TQuery, TResult>(
        TQuery query, 
        CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(IAsyncQueryHandler<TQuery, TResult>);
        var handler = _serviceProvider.GetService(handlerType);
        
        if (handler == null)
        {
            _logger.LogError(
                "No handler registered for query type {QueryType}",
                typeof(TQuery).Name);
            throw new InvalidOperationException(
                $"No handler registered for query type {typeof(TQuery).Name}");
        }
        
        try
        {
            return await ((IAsyncQueryHandler<TQuery, TResult>)handler)
                .HandleAsync(query, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error handling query of type {QueryType}",
                typeof(TQuery).Name);
            throw;
        }
    }
}
```

## Dependency Injection Setup

```csharp
// In your startup or service configuration
services.AddScoped<IQueryDispatcher, QueryDispatcher>();

// Register query handlers
services.AddScoped<IAsyncQueryHandler<GetAccountByIdQuery, AccountDto>, GetAccountByIdQueryHandler>();
services.AddScoped<IAsyncQueryHandler<ListAccountsQuery, AccountListResult>, ListAccountsQueryHandler>();
```

## Usage in API Controllers

```csharp
[ApiController]
[Route("api/accounts")]
public class AccountsController : ControllerBase
{
    private readonly IQueryDispatcher _queryDispatcher;
    
    public AccountsController(IQueryDispatcher queryDispatcher)
    {
        _queryDispatcher = queryDispatcher;
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult<AccountDto>> GetAccount(Guid id)
    {
        var query = new GetAccountByIdQuery(id);
        var result = await _queryDispatcher.DispatchAsync<GetAccountByIdQuery, AccountDto>(query);
        
        if (result == null)
            return NotFound();
            
        return Ok(result);
    }
    
    [HttpGet]
    public async Task<ActionResult<AccountListResult>> ListAccounts(
        [FromQuery] string customerName = null,
        [FromQuery] AccountStatus? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sortBy = "LastUpdated",
        [FromQuery] bool sortDescending = true)
    {
        var query = new ListAccountsQuery(
            customerName,
            status,
            pageNumber,
            pageSize,
            sortBy,
            sortDescending);
            
        var result = await _queryDispatcher.DispatchAsync<ListAccountsQuery, AccountListResult>(query);
        return Ok(result);
    }
}
```

## Best Practices

1. **Keep Queries Simple**: Queries should be simple data transfer objects without complex logic.
2. **Optimize for Read Performance**: Design read models and queries to minimize database round trips and optimize for specific query patterns.
3. **Use Asynchronous Handlers**: Prefer async query handlers to avoid blocking threads during I/O operations.
4. **Implement Pagination**: Always implement pagination for list queries to avoid performance issues with large result sets.
5. **Include Proper Error Handling**: Implement robust error handling in query handlers to provide meaningful error messages.
6. **Use Caching Where Appropriate**: Consider caching frequently accessed or expensive query results.
7. **Return DTOs, Not Domain Objects**: Always return DTOs (Data Transfer Objects) from query handlers, not domain objects or read models.
8. **Validate Queries**: Validate query parameters to ensure they meet business rules and constraints.
9. **Use Dependency Injection**: Register query handlers and repositories with a dependency injection container.
10. **Log Query Performance**: Include logging to track query performance and identify bottlenecks.

## Common Pitfalls

1. **Mixing Commands and Queries**: Avoid mixing state changes (commands) with data retrieval (queries).
2. **Over-fetching Data**: Avoid retrieving more data than needed for a specific use case.
3. **N+1 Query Problem**: Be careful not to execute N+1 queries when retrieving related data.
4. **Ignoring Eventual Consistency**: Remember that read models may be eventually consistent with the write model.
5. **Complex Query Logic**: Avoid putting complex business logic in query handlers; keep them focused on data retrieval.
6. **Missing Indexes**: Ensure appropriate database indexes for query performance.
7. **Returning Domain Objects**: Avoid returning domain objects or read models directly to clients.

## Related Components

- [ReadModelBase](./read-model-base.md): Base class for read models queried by query handlers
- [IReadModelRepository](./iread-model-repository.md): Interface for repositories that store and retrieve read models
- [Event](./event.md): Base class for domain events that trigger read model updates
- [Command](./command.md): Base class for commands that trigger state changes

---

**Navigation**:
- [← Previous: IReadModelRepository](./iread-model-repository.md)
- [↑ Back to Top](#query-handling)
- [→ Next: Event Subscription](./event-subscription.md)
