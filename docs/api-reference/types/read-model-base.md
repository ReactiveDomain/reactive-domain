# ReadModelBase

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

`ReadModelBase` is a foundational class in Reactive Domain that provides core functionality for implementing read models in a CQRS architecture.

## Overview

Read models in Reactive Domain represent the query side of the CQRS pattern. They are optimized for querying and provide a denormalized view of the domain data. The `ReadModelBase` class provides a common foundation for implementing read models with consistent behavior.

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

## Usage

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

## Integration with Event Handlers

Read models are typically updated by event handlers that subscribe to domain events:

```csharp
public class AccountEventHandler : IEventHandler<AccountCreated>, IEventHandler<FundsDeposited>
{
    private readonly IReadModelRepository<AccountSummary> _repository;
    
    public AccountEventHandler(IReadModelRepository<AccountSummary> repository)
    {
        _repository = repository;
    }
    
    public void Handle(AccountCreated @event)
    {
        var accountSummary = new AccountSummary(@event.AccountId);
        accountSummary.Update(@event.AccountNumber, @event.CustomerName, 0);
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

## Best Practices

1. **Keep Read Models Focused**: Each read model should serve a specific query scenario
2. **Immutable Properties**: Make properties private set to ensure they are only modified through well-defined methods
3. **Denormalization**: Denormalize data to optimize for query performance
4. **Eventual Consistency**: Remember that read models are eventually consistent with the write model
5. **Versioning**: Consider adding version information to handle schema evolution

## Common Pitfalls

1. **Business Logic in Read Models**: Avoid putting business logic in read models
2. **Complex Read Models**: Keep read models simple and focused on query requirements
3. **Missing Event Handlers**: Ensure all relevant events have handlers to update read models
4. **Ignoring Performance**: Design read models with query performance in mind

## Related Components

- [IReadModelRepository](./iread-model-repository.md): Interface for storing and retrieving read models
- [EventHandler](./event-handler.md): Handlers for updating read models based on domain events
- [IEvent](./ievent.md): Interface for domain events that trigger read model updates

---

**Navigation**:
- [← Previous: IRepository](./irepository.md)
- [↑ Back to Top](#readmodelbase)
- [→ Next: IReadModelRepository](./iread-model-repository.md)
