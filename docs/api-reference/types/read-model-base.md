# ReadModelBase

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

`ReadModelBase` is a foundational class in Reactive Domain that provides core functionality for implementing read models in a CQRS architecture.

## Overview

Read models in Reactive Domain represent the query side of the CQRS (Command Query Responsibility Segregation) pattern. They are specifically optimized for querying and provide a denormalized view of the domain data. The `ReadModelBase` class provides a common foundation for implementing read models with consistent behavior and identity management.

In a mature CQRS architecture, read models are completely separate from the write models (aggregates) and are designed to efficiently answer specific queries. Unlike aggregates that focus on maintaining consistency and enforcing business rules, read models focus on providing fast and efficient access to data in the exact shape needed by the UI or API consumers.

Key characteristics of read models include:

1. **Purpose-Built for Queries**: Designed specifically to answer particular questions or provide specific views of data
2. **Denormalized Structure**: Contains pre-computed, flattened data to eliminate the need for joins or complex transformations at query time
3. **Eventually Consistent**: Updated asynchronously in response to domain events, which means they may temporarily lag behind the write model
4. **Optimized for Reading**: Schema and storage mechanism chosen based on read patterns rather than normalization or write concerns
5. **Multiple Representations**: The same domain concept may be represented in multiple read models, each optimized for different query scenarios

Read models are typically updated by event handlers that subscribe to domain events raised by aggregates. This creates a clear separation between the write and read sides of the application, allowing each to be optimized for its specific purpose.

## Class Definition

```csharp
public abstract class ReadModelBase
{
    public Guid Id { get; protected set; }
    
    protected ReadModelBase(Guid id)
    {
        Id = id;
    }
    
    protected ReadModelBase()
    {
    }
}
```

## Key Features

- **Identity Management**: Provides a standard `Id` property for uniquely identifying read models, typically corresponding to an aggregate ID or other domain entity
- **Base Functionality**: Serves as a foundation for all read model implementations, providing common structure and behavior
- **Consistency**: Ensures consistent implementation patterns across different read models in the application
- **Separation of Concerns**: Facilitates the clear separation between read and write models in CQRS architecture
- **Optimized for Queries**: Designed to be efficient for read operations with a structure that matches query requirements
- **Extensibility**: Easily extended with additional properties and methods specific to each read model type
- **Serialization Support**: Simple structure makes serialization and deserialization straightforward for various storage mechanisms

## Usage

### Creating a Basic Read Model

To create a read model, inherit from `ReadModelBase` and add properties specific to your domain and query requirements:

```csharp
public class AccountSummary : ReadModelBase
{
    // Properties designed for efficient querying
    public string AccountNumber { get; private set; }
    public string CustomerName { get; private set; }
    public decimal Balance { get; private set; }
    public AccountStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime LastUpdated { get; private set; }
    
    // Additional properties for filtering and sorting
    public string CustomerEmail { get; private set; }
    public AccountType AccountType { get; private set; }
    public string BranchCode { get; private set; }
    
    // Constructor with required ID
    public AccountSummary(Guid id) : base(id)
    {
        // Initialize collections or default values if needed
    }
    
    // Update method with clear parameters
    public void Update(
        string accountNumber, 
        string customerName, 
        string customerEmail,
        decimal balance, 
        AccountStatus status,
        AccountType accountType,
        string branchCode)
    {
        AccountNumber = accountNumber;
        CustomerName = customerName;
        CustomerEmail = customerEmail;
        Balance = balance;
        Status = status;
        AccountType = accountType;
        BranchCode = branchCode;
        LastUpdated = DateTime.UtcNow;
    }
    
    // Specialized update methods for specific changes
    public void UpdateBalance(decimal newBalance)
    {
        Balance = newBalance;
        LastUpdated = DateTime.UtcNow;
    }
    
    public void UpdateStatus(AccountStatus newStatus)
    {
        Status = newStatus;
        LastUpdated = DateTime.UtcNow;
    }
}
```

### Creating a Specialized Query-Focused Read Model

Create read models that are specifically designed for particular query scenarios:

```csharp
// Read model optimized for account search functionality
public class AccountSearchResult : ReadModelBase
{
    // Properties needed for search results
    public string AccountNumber { get; private set; }
    public string CustomerName { get; private set; }
    public string CustomerEmail { get; private set; }
    public AccountType AccountType { get; private set; }
    public AccountStatus Status { get; private set; }
    public decimal Balance { get; private set; }
    public DateTime CreatedAt { get; private set; }
    
    // Searchable fields (indexed in the database)
    public string SearchText { get; private set; }
    
    public AccountSearchResult(Guid id) : base(id)
    {
    }
    
    public void Update(
        string accountNumber,
        string customerName,
        string customerEmail,
        AccountType accountType,
        AccountStatus status,
        decimal balance,
        DateTime createdAt)
    {
        AccountNumber = accountNumber;
        CustomerName = customerName;
        CustomerEmail = customerEmail;
        AccountType = accountType;
        Status = status;
        Balance = balance;
        CreatedAt = createdAt;
        
        // Create a combined search text for full-text search
        SearchText = $"{accountNumber} {customerName} {customerEmail} {accountType} {status}";
    }
}
```

### Creating an Aggregated Dashboard Read Model

For complex UI requirements, create read models that pre-aggregate data from multiple sources:

```csharp
public class CustomerDashboard : ReadModelBase
{
    // Customer information
    public string CustomerName { get; private set; }
    public string Email { get; private set; }
    public string PhoneNumber { get; private set; }
    public CustomerStatus Status { get; private set; }
    
    // Pre-calculated aggregates
    public decimal TotalBalance { get; private set; }
    public decimal TotalSavingsBalance { get; private set; }
    public decimal TotalCheckingBalance { get; private set; }
    public int AccountCount { get; private set; }
    public DateTime LastActivity { get; private set; }
    
    // Related entities (denormalized)
    public List<DashboardAccountSummary> Accounts { get; private set; }
    public List<DashboardTransactionSummary> RecentTransactions { get; private set; }
    
    // Constructor
    public CustomerDashboard(Guid customerId) : base(customerId)
    {
        Accounts = new List<DashboardAccountSummary>();
        RecentTransactions = new List<DashboardTransactionSummary>();
    }
    
    // Update methods
    public void UpdateCustomerInfo(string name, string email, string phoneNumber, CustomerStatus status)
    {
        CustomerName = name;
        Email = email;
        PhoneNumber = phoneNumber;
        Status = status;
    }
    
    public void AddOrUpdateAccount(DashboardAccountSummary account)
    {
        var existingAccount = Accounts.FirstOrDefault(a => a.AccountId == account.AccountId);
        if (existingAccount != null)
        {
            var index = Accounts.IndexOf(existingAccount);
            Accounts[index] = account;
        }
        else
        {
            Accounts.Add(account);
        }
        
        RecalculateBalances();
        AccountCount = Accounts.Count;
    }
    
    public void AddTransaction(DashboardTransactionSummary transaction)
    {
        // Add the transaction and keep only the most recent ones
        RecentTransactions.Add(transaction);
        RecentTransactions = RecentTransactions
            .OrderByDescending(t => t.Timestamp)
            .Take(10)
            .ToList();
            
        // Update the last activity timestamp
        if (transaction.Timestamp > LastActivity)
        {
            LastActivity = transaction.Timestamp;
        }
    }
    
    private void RecalculateBalances()
    {
        TotalCheckingBalance = Accounts
            .Where(a => a.AccountType == AccountType.Checking)
            .Sum(a => a.Balance);
            
        TotalSavingsBalance = Accounts
            .Where(a => a.AccountType == AccountType.Savings)
            .Sum(a => a.Balance);
            
        TotalBalance = TotalCheckingBalance + TotalSavingsBalance;
    }
}

// Simplified nested class for the dashboard
public class DashboardAccountSummary
{
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; }
    public AccountType AccountType { get; set; }
    public decimal Balance { get; set; }
    public AccountStatus Status { get; set; }
}

// Simplified nested class for the dashboard
public class DashboardTransactionSummary
{
    public Guid TransactionId { get; set; }
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; }
    public TransactionType Type { get; set; }
    public DateTime Timestamp { get; set; }
}
```

## Integration with Event Handlers

There are several patterns for integrating read models with event handlers in Reactive Domain. Here are the most common approaches, each with its own advantages:

### Pattern 1: Dedicated Projection Classes

This pattern separates the read model (data container) from the event handling logic, following the Single Responsibility Principle:

```csharp
// The read model is just a data container
public class AccountSummary : ReadModelBase
{
    public string AccountNumber { get; private set; }
    public string CustomerName { get; private set; }
    public decimal Balance { get; private set; }
    public AccountStatus Status { get; private set; }
    public DateTime LastUpdated { get; private set; }
    
    public AccountSummary(Guid id) : base(id) { }
    
    // Update methods
    public void Update(string accountNumber, string customerName, decimal balance, AccountStatus status)
    {
        AccountNumber = accountNumber;
        CustomerName = customerName;
        Balance = balance;
        Status = status;
        LastUpdated = DateTime.UtcNow;
    }
    
    public void UpdateBalance(decimal newBalance)
    {
        Balance = newBalance;
        LastUpdated = DateTime.UtcNow;
    }
    
    public void UpdateStatus(AccountStatus newStatus)
    {
        Status = newStatus;
        LastUpdated = DateTime.UtcNow;
    }
}

// Separate projection class handles the events
public class AccountSummaryProjection : 
    IEventHandler<AccountCreated>,
    IEventHandler<FundsDeposited>,
    IEventHandler<FundsWithdrawn>,
    IEventHandler<AccountClosed>
{
    private readonly IReadModelRepository<AccountSummary> _repository;
    private readonly ILogger<AccountSummaryProjection> _logger;
    
    public AccountSummaryProjection(
        IReadModelRepository<AccountSummary> repository,
        ILogger<AccountSummaryProjection> logger)
    {
        _repository = repository;
        _logger = logger;
    }
    
    public void Handle(AccountCreated @event)
    {
        try
        {
            // Create a new read model
            var accountSummary = new AccountSummary(@event.AccountId);
            accountSummary.Update(
                @event.AccountNumber,
                @event.CustomerName,
                @event.InitialBalance,
                AccountStatus.Active);
            
            // Save the read model
            _repository.Save(accountSummary);
            
            _logger.LogInformation(
                "Created account summary for account {AccountId}", 
                @event.AccountId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creating account summary for account {AccountId}",
                @event.AccountId);
            throw; // Rethrow to allow the event processing system to handle it
        }
    }
    
    public void Handle(FundsDeposited @event)
    {
        try
        {
            // Get the existing read model
            var accountSummary = _repository.GetById(@event.AccountId);
            if (accountSummary == null)
            {
                _logger.LogWarning(
                    "Account summary not found for account {AccountId} when processing FundsDeposited",
                    @event.AccountId);
                return;
            }
            
            // Update the balance
            accountSummary.UpdateBalance(accountSummary.Balance + @event.Amount);
            
            // Save the updated read model
            _repository.Save(accountSummary);
            
            _logger.LogDebug(
                "Updated account summary balance for account {AccountId}, new balance: {Balance}",
                @event.AccountId,
                accountSummary.Balance);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error updating account summary for account {AccountId} when processing FundsDeposited",
                @event.AccountId);
            throw;
        }
    }
    
    public void Handle(FundsWithdrawn @event)
    {
        try
        {
            // Get the existing read model
            var accountSummary = _repository.GetById(@event.AccountId);
            if (accountSummary == null)
            {
                _logger.LogWarning(
                    "Account summary not found for account {AccountId} when processing FundsWithdrawn",
                    @event.AccountId);
                return;
            }
            
            // Update the balance
            accountSummary.UpdateBalance(accountSummary.Balance - @event.Amount);
            
            // Save the updated read model
            _repository.Save(accountSummary);
            
            _logger.LogDebug(
                "Updated account summary balance for account {AccountId}, new balance: {Balance}",
                @event.AccountId,
                accountSummary.Balance);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error updating account summary for account {AccountId} when processing FundsWithdrawn",
                @event.AccountId);
            throw;
        }
    }
    
    public void Handle(AccountClosed @event)
    {
        try
        {
            // Get the existing read model
            var accountSummary = _repository.GetById(@event.AccountId);
            if (accountSummary == null)
            {
                _logger.LogWarning(
                    "Account summary not found for account {AccountId} when processing AccountClosed",
                    @event.AccountId);
                return;
            }
            
            // Update the status
            accountSummary.UpdateStatus(AccountStatus.Closed);
            
            // Save the updated read model
            _repository.Save(accountSummary);
            
            _logger.LogInformation(
                "Updated account summary status to Closed for account {AccountId}",
                @event.AccountId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error updating account summary for account {AccountId} when processing AccountClosed",
                @event.AccountId);
            throw;
        }
    }
}
```

### Pattern 2: Self-Handling Read Models

In simpler scenarios, the read model can implement the event handler interfaces directly:

```csharp
public class SimpleAccountSummary : ReadModelBase,
    IEventHandler<AccountCreated>,
    IEventHandler<FundsDeposited>,
    IEventHandler<FundsWithdrawn>,
    IEventHandler<AccountClosed>
{
    private readonly IReadModelRepository<SimpleAccountSummary> _repository;
    
    public string AccountNumber { get; private set; }
    public string CustomerName { get; private set; }
    public decimal Balance { get; private set; }
    public AccountStatus Status { get; private set; }
    public DateTime LastUpdated { get; private set; }
    
    // Constructor for creating a new read model instance
    public SimpleAccountSummary(Guid id, IReadModelRepository<SimpleAccountSummary> repository) : base(id)
    {
        _repository = repository;
    }
    
    // Event handlers
    public void Handle(AccountCreated @event)
    {
        AccountNumber = @event.AccountNumber;
        CustomerName = @event.CustomerName;
        Balance = @event.InitialBalance;
        Status = AccountStatus.Active;
        LastUpdated = DateTime.UtcNow;
        
        _repository.Save(this);
    }
    
    public void Handle(FundsDeposited @event)
    {
        Balance += @event.Amount;
        LastUpdated = DateTime.UtcNow;
        
        _repository.Save(this);
    }
    
    public void Handle(FundsWithdrawn @event)
    {
        Balance -= @event.Amount;
        LastUpdated = DateTime.UtcNow;
        
        _repository.Save(this);
    }
    
    public void Handle(AccountClosed @event)
    {
        Status = AccountStatus.Closed;
        LastUpdated = DateTime.UtcNow;
        
        _repository.Save(this);
    }
}
```

### Pattern 3: Generic Projection Infrastructure

For more complex systems, a generic projection infrastructure can be beneficial:

```csharp
// Generic projection base class
public abstract class Projection<TReadModel> where TReadModel : ReadModelBase
{
    protected readonly IReadModelRepository<TReadModel> Repository;
    protected readonly ILogger Logger;
    
    protected Projection(IReadModelRepository<TReadModel> repository, ILogger logger)
    {
        Repository = repository;
        Logger = logger;
    }
    
    protected TReadModel GetOrCreateReadModel(Guid id, Func<TReadModel> factory = null)
    {
        var readModel = Repository.GetById(id);
        if (readModel == null && factory != null)
        {
            readModel = factory();
        }
        return readModel;
    }
    
    protected void SaveReadModel(TReadModel readModel, string operation)
    {
        try
        {
            Repository.Save(readModel);
            Logger.LogDebug(
                "{Operation} read model {ReadModelType} with ID {ReadModelId}",
                operation,
                typeof(TReadModel).Name,
                readModel.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "Error saving read model {ReadModelType} with ID {ReadModelId}",
                typeof(TReadModel).Name,
                readModel.Id);
            throw;
        }
    }
}

// Concrete projection implementation
public class AccountSummaryProjection : Projection<AccountSummary>,
    IEventHandler<AccountCreated>,
    IEventHandler<FundsDeposited>,
    IEventHandler<FundsWithdrawn>,
    IEventHandler<AccountClosed>
{
    public AccountSummaryProjection(
        IReadModelRepository<AccountSummary> repository,
        ILogger<AccountSummaryProjection> logger)
        : base(repository, logger)
    {
    }
    
    public void Handle(AccountCreated @event)
    {
        var readModel = new AccountSummary(@event.AccountId);
        readModel.Update(
            @event.AccountNumber,
            @event.CustomerName,
            @event.InitialBalance,
            AccountStatus.Active);
            
        SaveReadModel(readModel, "Created");
    }
    
    public void Handle(FundsDeposited @event)
    {
        var readModel = GetOrCreateReadModel(@event.AccountId);
        if (readModel == null) return;
        
        readModel.UpdateBalance(readModel.Balance + @event.Amount);
        SaveReadModel(readModel, "Updated");
    }
    
    public void Handle(FundsWithdrawn @event)
    {
        var readModel = GetOrCreateReadModel(@event.AccountId);
        if (readModel == null) return;
        
        readModel.UpdateBalance(readModel.Balance - @event.Amount);
        SaveReadModel(readModel, "Updated");
    }
    
    public void Handle(AccountClosed @event)
    {
        var readModel = GetOrCreateReadModel(@event.AccountId);
        if (readModel == null) return;
        
        readModel.UpdateStatus(AccountStatus.Closed);
        SaveReadModel(readModel, "Updated");
    }
}
```

## Registering Read Models with the Event Bus

To make read models receive events, they need to be registered with the event bus. Here's how to register a read model as an event handler:

```csharp
public class ReadModelRegistration
{
    private readonly IEventBus _eventBus;
    private readonly IReadModelRepository<AccountSummaryReadModel> _repository;
    
    public ReadModelRegistration(IEventBus eventBus, IReadModelRepository<AccountSummaryReadModel> repository)
    {
        _eventBus = eventBus;
        _repository = repository;
    }
    
    public void RegisterReadModels()
    {
        // Create and register the read model for each account
        var accounts = GetAllAccountIds();
        foreach (var accountId in accounts)
        {
            // Create or retrieve the read model
            var readModel = _repository.GetById(accountId) ?? 
                new AccountSummaryReadModel(accountId, _repository);
            
            // Register the read model as an event handler
            _eventBus.Subscribe<AccountCreated>(readModel);
            _eventBus.Subscribe<FundsDeposited>(readModel);
            _eventBus.Subscribe<FundsWithdrawn>(readModel);
            _eventBus.Subscribe<AccountClosed>(readModel);
        }
    }
    
    private IEnumerable<Guid> GetAllAccountIds()
    {
        // In a real implementation, this would retrieve all account IDs
        // from the event store or another source
        return new List<Guid>();
    }
}
```

## Querying Read Models

Read models are designed to be efficiently queried. Here's an example of a query service that uses read models:

```csharp
public class AccountQueryService
{
    private readonly IReadModelRepository<AccountSummary> _accountRepository;
    private readonly IReadModelRepository<CustomerDashboard> _dashboardRepository;
    
    public AccountQueryService(
        IReadModelRepository<AccountSummary> accountRepository,
        IReadModelRepository<CustomerDashboard> dashboardRepository)
    {
        _accountRepository = accountRepository;
        _dashboardRepository = dashboardRepository;
    }
    
    public AccountSummary GetAccountSummary(Guid accountId)
    {
        return _accountRepository.GetById(accountId);
    }
    
    public CustomerDashboard GetCustomerDashboard(Guid customerId)
    {
        return _dashboardRepository.GetById(customerId);
    }
    
    public IEnumerable<AccountSummary> FindAccountsByCustomerName(string customerName)
    {
        // In a real implementation, this would use a more efficient query mechanism
        // This is just a placeholder for the example
        return _accountRepository.GetAll()
            .Where(a => a.CustomerName.Contains(customerName, StringComparison.OrdinalIgnoreCase));
    }
    
    public decimal GetTotalBalanceForCustomer(Guid customerId)
    {
        var dashboard = _dashboardRepository.GetById(customerId);
        return dashboard?.TotalBalance ?? 0;
    }
}
```

## Persistence Strategies

Read models can be persisted in various ways, depending on the query requirements:

### In-Memory Storage

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

### Database Storage

```csharp
public class SqlReadModelRepository<T> : IReadModelRepository<T> where T : ReadModelBase
{
    private readonly string _connectionString;
    
    public SqlReadModelRepository(string connectionString)
    {
        _connectionString = connectionString;
    }
    
    public T GetById(Guid id)
    {
        // Implementation using ADO.NET, Dapper, Entity Framework, etc.
        // ...
    }
    
    public IEnumerable<T> GetAll()
    {
        // Implementation using ADO.NET, Dapper, Entity Framework, etc.
        // ...
    }
    
    public void Save(T item)
    {
        // Implementation using ADO.NET, Dapper, Entity Framework, etc.
        // ...
    }
    
    public void Delete(Guid id)
    {
        // Implementation using ADO.NET, Dapper, Entity Framework, etc.
        // ...
    }
}
```

## Best Practices

### Design Principles

1. **Purpose-Driven Design**: Design read models based on specific query requirements rather than mirroring the domain model. Start by identifying the queries your application needs to support, then design read models optimized for those queries.

   ```csharp
   // Good: Designed specifically for a dashboard view
   public class CustomerDashboardReadModel : ReadModelBase
   {
       public string CustomerName { get; private set; }
       public int TotalAccounts { get; private set; }
       public decimal TotalBalance { get; private set; }
       public DateTime LastActivity { get; private set; }
       public List<RecentTransaction> RecentTransactions { get; private set; }
   }
   ```

2. **Single Responsibility**: Each read model should serve a specific query scenario or related set of queries. Don't try to make a read model serve too many different purposes.

3. **Immutable Properties**: Make properties with private setters to ensure they are only modified through well-defined methods. This helps maintain the integrity of the read model.

4. **Denormalization for Performance**: Precompute and store derived data to eliminate the need for complex queries or joins at query time. This may include:
   - Storing calculated totals and counts
   - Duplicating data across multiple read models
   - Storing data in the exact format needed for display

5. **Explicit Update Methods**: Provide clear, well-named methods for updating the read model rather than directly modifying properties.

   ```csharp
   // Good: Clear update methods with specific purposes
   public void UpdateBalance(decimal newBalance)
   {
       Balance = newBalance;
       LastUpdated = DateTime.UtcNow;
   }
   
   public void UpdateStatus(AccountStatus newStatus)
   {
       Status = newStatus;
       LastUpdated = DateTime.UtcNow;
   }
   ```

### Implementation Strategies

6. **Separate Projection Classes**: Consider separating read models (data containers) from projection logic (event handlers) to maintain a clear separation of concerns.

7. **Idempotent Updates**: Design read model updates to be idempotent, as events may be processed multiple times due to retries or replay.

   ```csharp
   // Good: Idempotent update that works regardless of how many times it's called
   public void Handle(AccountClosed @event)
   {
       var readModel = _repository.GetById(@event.AccountId);
       if (readModel != null && readModel.Status != AccountStatus.Closed)
       {
           readModel.UpdateStatus(AccountStatus.Closed);
           _repository.Save(readModel);
       }
   }
   ```

8. **Error Handling**: Implement robust error handling in projections to prevent a single failed update from breaking the entire projection process.

9. **Versioning Strategy**: Include version information in read models to handle schema evolution over time.

   ```csharp
   public class VersionedReadModel : ReadModelBase
   {
       public int SchemaVersion { get; private set; } = 1;
       
       // Other properties...
       
       public void UpgradeSchema(int targetVersion)
       {
           if (SchemaVersion < targetVersion)
           {
               // Perform schema migration logic
               SchemaVersion = targetVersion;
           }
       }
   }
   ```

### Infrastructure Considerations

10. **Storage Technology Selection**: Choose storage technologies based on query patterns rather than write patterns. Different read models may use different storage technologies:
    - Relational databases for complex queries with joins
    - Document databases for hierarchical data
    - Search engines for full-text search
    - In-memory databases for high-performance needs

11. **Asynchronous Projections**: Process events asynchronously to update read models, especially in high-throughput systems.

    ```csharp
    public async Task HandleAsync(AccountCreated @event)
    {
        var readModel = new AccountSummary(@event.AccountId);
        // Update properties...
        await _repository.SaveAsync(readModel);
    }
    ```

12. **Rebuild Capability**: Design your system to be able to rebuild read models from event streams when needed. This is crucial for:
    - Fixing corrupted read models
    - Creating new read models from historical events
    - Migrating to new storage technologies

13. **Caching Strategy**: Implement appropriate caching for frequently accessed read models, with proper cache invalidation when updates occur.

14. **Monitoring and Observability**: Add monitoring to track:
    - Projection performance and throughput
    - Lag between write model updates and read model updates
    - Failed projections and error rates
    - Read model query performance

## Common Pitfalls

### Design Issues

1. **Business Logic in Read Models**: Read models should not contain business rules or validation logic. Keep them focused on data representation for queries.

   ```csharp
   // Bad: Business logic in read model
   public class AccountReadModel : ReadModelBase
   {
       public decimal Balance { get; private set; }
       
       public void Withdraw(decimal amount)
       {
           // Business logic doesn't belong here
           if (amount <= 0)
               throw new ArgumentException("Amount must be positive");
               
           if (Balance < amount)
               throw new InvalidOperationException("Insufficient funds");
               
           Balance -= amount;
       }
   }
   ```

2. **Mirror Image of Aggregates**: Avoid simply copying the structure of your domain aggregates. Read models should be designed for query efficiency, not domain modeling.

3. **One-Size-Fits-All Models**: Don't try to create a single read model that serves all query needs. This leads to bloated models that are inefficient for any specific query.

4. **Tight Coupling to Domain Model**: Read models should not depend on or reference domain aggregates. Keep them completely separate.

5. **Ignoring Query Patterns**: Failing to design read models based on actual query requirements leads to inefficient queries and poor performance.

### Implementation Pitfalls

6. **Missing Event Handlers**: Ensure all relevant events have handlers to update read models. Missing handlers lead to inconsistent or incomplete read models.

7. **Non-Idempotent Updates**: Event handlers that aren't idempotent can cause data corruption when events are processed multiple times.

8. **Synchronous Updates in Request Pipeline**: Updating read models synchronously within the command handling process can slow down command processing and reduce system throughput.

9. **Neglecting Database Indexes**: Failing to create appropriate indexes for query patterns can severely impact performance.

10. **Inefficient Queries**: Using complex queries against read models defeats the purpose of having denormalized models optimized for reading.

### Operational Challenges

11. **Ignoring Eventual Consistency**: Failing to design your UI and API consumers to handle eventual consistency can lead to confusing user experiences.

12. **No Rebuild Strategy**: Not having a way to rebuild read models from event streams makes it difficult to recover from data corruption or create new read model types.

13. **Lack of Monitoring**: Without proper monitoring, it's difficult to detect when read models are out of sync with the write model or when projections are failing.

14. **Inadequate Error Handling**: Poor error handling in projections can cause entire projection processes to fail, leading to stale or incomplete read models.

15. **Unbounded Growth**: Not implementing strategies for managing the growth of read models (archiving, partitioning, etc.) can lead to performance degradation over time.

## Related Components

- [IReadModelRepository](./iread-model-repository.md): Interface for storing and retrieving read models
- [EventHandler](./event-handler.md): Handlers for updating read models based on domain events
- [IEvent](./ievent.md): Interface for domain events that trigger read model updates
- [Command](./command.md): Base class for command messages that trigger state changes
- [Event](./event.md): Base class for event messages that update read models
- [ICorrelatedMessage](./icorrelated-message.md): Interface for tracking message correlation
- [AggregateRoot](./aggregate-root.md): The write-side counterpart to read models in CQRS
- [MessageBuilder](./message-builder.md): Helps create correlated events that update read models

For a comprehensive view of how these components interact, see the [Key Component Relationships](../../architecture.md#key-component-relationships) section in the Architecture Guide.

---

**Navigation**:
- [← Previous: IRepository](./irepository.md)
- [↑ Back to Top](#readmodelbase)
- [→ Next: IReadModelRepository](./iread-model-repository.md)
