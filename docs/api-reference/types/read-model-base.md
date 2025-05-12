# ReadModelBase

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

`ReadModelBase` is a foundational class in Reactive Domain that provides core functionality for implementing read models in a CQRS architecture.

## Overview

Read models in Reactive Domain represent the query side of the CQRS pattern. They are optimized for querying and provide a denormalized view of the domain data. The `ReadModelBase` class provides a common foundation for implementing read models with consistent behavior.

In a CQRS architecture, read models are separate from the write models (aggregates) and are specifically designed to efficiently answer queries. They typically contain denormalized data that is shaped according to the specific needs of the UI or API consumers. Read models are updated by event handlers in response to domain events raised by aggregates, creating an eventually consistent view of the domain state.

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

- **Identity Management**: Provides a standard `Id` property for uniquely identifying read models
- **Base Functionality**: Serves as a foundation for all read model implementations
- **Consistency**: Ensures consistent implementation patterns across different read models
- **Separation of Concerns**: Facilitates the separation between read and write models in CQRS
- **Optimized for Queries**: Designed to be efficient for read operations

## Usage

### Creating a Basic Read Model

To create a read model, inherit from `ReadModelBase` and add properties specific to your domain:

```csharp
public class AccountSummary : ReadModelBase
{
    public string AccountNumber { get; private set; }
    public string CustomerName { get; private set; }
    public decimal Balance { get; private set; }
    public DateTime LastUpdated { get; private set; }
    
    public AccountSummary(Guid id) : base(id)
    {
    }
    
    public void Update(string accountNumber, string customerName, decimal balance)
    {
        AccountNumber = accountNumber;
        CustomerName = customerName;
        Balance = balance;
        LastUpdated = DateTime.UtcNow;
    }
}
```

### Creating a More Complex Read Model

For more complex scenarios, you can create read models that aggregate data from multiple sources:

```csharp
public class CustomerDashboard : ReadModelBase
{
    public string CustomerName { get; private set; }
    public string Email { get; private set; }
    public decimal TotalBalance { get; private set; }
    public int AccountCount { get; private set; }
    public List<AccountSummary> Accounts { get; private set; }
    public List<TransactionSummary> RecentTransactions { get; private set; }
    
    public CustomerDashboard(Guid customerId) : base(customerId)
    {
        Accounts = new List<AccountSummary>();
        RecentTransactions = new List<TransactionSummary>();
    }
    
    public void UpdateCustomerInfo(string name, string email)
    {
        CustomerName = name;
        Email = email;
    }
    
    public void AddAccount(AccountSummary account)
    {
        Accounts.Add(account);
        AccountCount = Accounts.Count;
        RecalculateTotalBalance();
    }
    
    public void UpdateAccount(AccountSummary updatedAccount)
    {
        var existingAccount = Accounts.FirstOrDefault(a => a.Id == updatedAccount.Id);
        if (existingAccount != null)
        {
            var index = Accounts.IndexOf(existingAccount);
            Accounts[index] = updatedAccount;
            RecalculateTotalBalance();
        }
    }
    
    public void AddTransaction(TransactionSummary transaction)
    {
        RecentTransactions.Add(transaction);
        RecentTransactions = RecentTransactions
            .OrderByDescending(t => t.Timestamp)
            .Take(10)
            .ToList();
    }
    
    private void RecalculateTotalBalance()
    {
        TotalBalance = Accounts.Sum(a => a.Balance);
    }
}
```

## Integration with Event Handlers

In Reactive Domain, read models typically implement the event handler interfaces directly. This pattern allows the read model to handle its own updates in response to domain events. Here's how read models should be implemented to handle events:

```csharp
// The read model itself implements the event handler interfaces
public class AccountSummaryReadModel : ReadModelBase,
    IEventHandler<AccountCreated>,
    IEventHandler<FundsDeposited>,
    IEventHandler<FundsWithdrawn>,
    IEventHandler<AccountClosed>
{
    private readonly IReadModelRepository<AccountSummaryReadModel> _repository;
    
    public string AccountNumber { get; private set; }
    public string CustomerName { get; private set; }
    public decimal Balance { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime LastUpdated { get; private set; }
    
    // Constructor for creating a new read model instance
    public AccountSummaryReadModel(Guid id, IReadModelRepository<AccountSummaryReadModel> repository) : base(id)
    {
        _repository = repository;
    }
    
    // Event handler for AccountCreated
    public void Handle(AccountCreated @event)
    {
        // Update the read model state
        AccountNumber = @event.AccountNumber;
        CustomerName = @event.CustomerName;
        Balance = @event.InitialBalance;
        IsActive = true;
        LastUpdated = DateTime.UtcNow;
        
        // Save the updated read model
        _repository.Save(this);
    }
    
    // Event handler for FundsDeposited
    public void Handle(FundsDeposited @event)
    {
        // Ensure this is the correct account
        if (@event.AccountId == Id)
        {
            // Update the read model state
            Balance += @event.Amount;
            LastUpdated = DateTime.UtcNow;
            
            // Save the updated read model
            _repository.Save(this);
        }
    }
    
    // Event handler for FundsWithdrawn
    public void Handle(FundsWithdrawn @event)
    {
        // Ensure this is the correct account
        if (@event.AccountId == Id)
        {
            // Update the read model state
            Balance -= @event.Amount;
            LastUpdated = DateTime.UtcNow;
            
            // Save the updated read model
            _repository.Save(this);
        }
    }
    
    // Event handler for AccountClosed
    public void Handle(AccountClosed @event)
    {
        // Ensure this is the correct account
        if (@event.AccountId == Id)
        {
            // Update the read model state
            IsActive = false;
            LastUpdated = DateTime.UtcNow;
            
            // Save the updated read model
            _repository.Save(this);
        }
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

1. **Keep Read Models Focused**: Each read model should serve a specific query scenario
2. **Immutable Properties**: Make properties private set to ensure they are only modified through well-defined methods
3. **Denormalization**: Denormalize data to optimize for query performance
4. **Eventual Consistency**: Remember that read models are eventually consistent with the write model
5. **Versioning**: Consider adding version information to handle schema evolution
6. **Optimize for Reads**: Structure your read models to minimize the need for joins or complex queries
7. **Separate Storage**: Consider using different storage technologies for read and write models
8. **Rebuild Capability**: Design your system to be able to rebuild read models from event streams when needed
9. **Caching Strategy**: Implement appropriate caching for frequently accessed read models
10. **Monitoring**: Add monitoring to track the lag between write model updates and read model updates
11. **Idempotent Updates**: Ensure read model updates are idempotent, as events may be processed multiple times
12. **Event Handler Organization**: Organize event handlers by the read models they update rather than by event type

## Common Pitfalls

1. **Business Logic in Read Models**: Avoid putting business logic in read models
2. **Complex Read Models**: Keep read models simple and focused on query requirements
3. **Missing Event Handlers**: Ensure all relevant events have handlers to update read models
4. **Ignoring Performance**: Design read models with query performance in mind
5. **Tight Coupling**: Avoid coupling read models to domain aggregates
6. **Overloading**: Don't try to make a single read model serve too many different query scenarios
7. **Inconsistent Naming**: Maintain consistent naming conventions between events, commands, and read models
8. **Neglecting Indexes**: Ensure appropriate database indexes for efficient querying
9. **Synchronous Updates**: Be cautious about synchronously updating read models in high-throughput systems
10. **Ignoring Eventual Consistency**: Design your UI and API consumers to handle eventual consistency

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
