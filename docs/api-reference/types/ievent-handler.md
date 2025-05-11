# IEventHandler

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

`IEventHandler` is a core interface in Reactive Domain that defines the contract for components that handle domain events.

## Overview

Event handlers are a fundamental part of event-driven architectures, responsible for reacting to domain events and updating read models, triggering side effects, or initiating other processes. In Reactive Domain, the `IEventHandler` interface provides a standard way to define event handlers that can be registered with the event bus.

Event handlers are typically used to maintain read models that support queries in a CQRS architecture. They subscribe to domain events raised by aggregates and transform these events into denormalized data structures optimized for querying.

## Interface Definition

```csharp
public interface IEventHandler<in TEvent> where TEvent : IEvent
{
    void Handle(TEvent @event);
}
```

## Key Features

- **Type Safety**: Provides type-safe handling of specific event types
- **Single Responsibility**: Each handler implementation focuses on handling a specific event type
- **Decoupling**: Enables loose coupling between event producers (aggregates) and event consumers (read models)
- **Scalability**: Allows for independent scaling of read and write sides in a CQRS architecture
- **Extensibility**: Makes it easy to add new event handlers without modifying existing code

## Usage

### Basic Event Handler

Here's a simple example of an event handler that updates a read model when an account is created:

```csharp
public class AccountCreatedHandler : IEventHandler<AccountCreated>
{
    private readonly IReadModelRepository<AccountSummary> _repository;
    
    public AccountCreatedHandler(IReadModelRepository<AccountSummary> repository)
    {
        _repository = repository;
    }
    
    public void Handle(AccountCreated @event)
    {
        // Create a new read model from the event data
        var accountSummary = new AccountSummary(@event.AccountId);
        accountSummary.Update(@event.AccountNumber, @event.CustomerName, @event.InitialBalance);
        
        // Save the read model to the repository
        _repository.Save(accountSummary);
    }
}
```

### Handling Multiple Event Types

A class can implement multiple `IEventHandler<T>` interfaces to handle different event types:

```csharp
public class AccountEventHandler : 
    IEventHandler<AccountCreated>,
    IEventHandler<FundsDeposited>,
    IEventHandler<FundsWithdrawn>
{
    private readonly IReadModelRepository<AccountSummary> _repository;
    
    public AccountEventHandler(IReadModelRepository<AccountSummary> repository)
    {
        _repository = repository;
    }
    
    public void Handle(AccountCreated @event)
    {
        // Handle account creation
        var accountSummary = new AccountSummary(@event.AccountId);
        accountSummary.Update(@event.AccountNumber, @event.CustomerName, @event.InitialBalance);
        _repository.Save(accountSummary);
    }
    
    public void Handle(FundsDeposited @event)
    {
        // Handle funds deposit
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
    
    public void Handle(FundsWithdrawn @event)
    {
        // Handle funds withdrawal
        var accountSummary = _repository.GetById(@event.AccountId);
        if (accountSummary != null)
        {
            accountSummary.Update(
                accountSummary.AccountNumber,
                accountSummary.CustomerName,
                accountSummary.Balance - @event.Amount);
            _repository.Save(accountSummary);
        }
    }
}
```

### Registering Event Handlers

Event handlers are typically registered with an event bus during application startup:

```csharp
public void ConfigureEventHandlers(IEventBus eventBus, IReadModelRepository<AccountSummary> repository)
{
    var accountEventHandler = new AccountEventHandler(repository);
    
    // Register the handler for each event type it handles
    eventBus.Subscribe<AccountCreated>(accountEventHandler);
    eventBus.Subscribe<FundsDeposited>(accountEventHandler);
    eventBus.Subscribe<FundsWithdrawn>(accountEventHandler);
}
```

## Best Practices

1. **Idempotent Handlers**: Make event handlers idempotent so they can safely process the same event multiple times
2. **Error Handling**: Implement proper error handling to prevent a single failed event from stopping the processing of subsequent events
3. **Single Responsibility**: Keep handlers focused on a specific domain concept or read model
4. **Performance Consideration**: Keep handlers lightweight and fast to avoid blocking the event processing pipeline
5. **Logging**: Include appropriate logging to track event processing and troubleshoot issues
6. **Transaction Management**: Consider transaction boundaries when updating multiple read models or external systems
7. **Eventual Consistency**: Design systems to handle the eventual consistency inherent in event-driven architectures
8. **Testing**: Write unit tests for event handlers to ensure they correctly update read models

## Common Pitfalls

1. **Business Logic in Handlers**: Avoid putting domain business logic in event handlers; they should focus on updating read models
2. **Synchronous External Calls**: Be cautious about making synchronous calls to external systems from event handlers
3. **Ignoring Errors**: Failing to handle errors properly can lead to lost events or inconsistent read models
4. **Over-normalization**: Denormalize data in read models appropriately for query efficiency
5. **Tight Coupling**: Avoid tightly coupling event handlers to specific aggregate implementations
6. **Missing Events**: Ensure handlers are registered for all relevant events to maintain complete read models
7. **Order Dependency**: Be careful about assuming a specific order of event processing

## Related Components

- [IEvent](./ievent.md): Interface for domain events processed by event handlers
- [ReadModelBase](./read-model-base.md): Base class for read models updated by event handlers
- [IReadModelRepository](./iread-model-repository.md): Interface for storing and retrieving read models
- [Event](./event.md): Base class for domain events
- [AggregateRoot](./aggregate-root.md): Domain entities that raise events processed by event handlers
- [ICorrelatedMessage](./icorrelated-message.md): Interface for tracking message correlation

---

**Navigation**:
- [← Previous: IEvent](./ievent.md)
- [↑ Back to Top](#ieventhandler)
- [→ Next: ReadModelBase](./read-model-base.md)
