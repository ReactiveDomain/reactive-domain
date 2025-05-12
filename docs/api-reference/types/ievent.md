# IEvent Interface

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

## Overview

The `IEvent` interface is a marker interface that defines the contract for events in Reactive Domain. Events represent facts that have occurred in the system and are a fundamental building block in Event Sourcing and CQRS (Command Query Responsibility Segregation) architectures.

In Reactive Domain, events are immutable messages that describe something that has happened in the domain. They are used to record state changes, update read models, trigger processes, and provide an audit trail of all changes to the system.

## Events in Event Sourcing

In an Event Sourcing architecture, events play a crucial role:

1. **State Changes**: Events represent all state changes in the system
2. **Event Stream**: The sequence of events forms an event stream that represents the history of an entity
3. **State Reconstruction**: The current state of an entity can be reconstructed by replaying its events
4. **Temporal Queries**: The state of an entity at any point in time can be determined by replaying events up to that point
5. **Audit Trail**: Events provide a complete audit trail of all changes to the system

Events are distinct from commands (which represent intentions to change state) in that events represent facts that have already occurred and cannot be rejected.

**Namespace**: `ReactiveDomain.Messaging`  
**Assembly**: `ReactiveDomain.Messaging.dll`

```csharp
public interface IEvent : IMessage
{
}
```

## Inheritance

The `IEvent` interface inherits from the `IMessage` interface, which provides the base contract for all messages in Reactive Domain.

```
IMessage
  ↑
IEvent
```

## Usage

The `IEvent` interface is typically used as a marker interface to identify event messages in the system. Events are usually implemented as concrete classes that inherit from the `Event` base class, which provides common functionality for events.

### Basic Event Implementation

```csharp
// Define an event
public class AccountCreated : Event
{
    public Guid AccountId { get; }
    public string AccountNumber { get; }
    public decimal InitialDeposit { get; }
    
    public AccountCreated(Guid accountId, string accountNumber, decimal initialDeposit)
    {
        AccountId = accountId;
        AccountNumber = accountNumber;
        InitialDeposit = initialDeposit;
    }
}

// Define an event handler
public class AccountReadModelUpdater : IEventHandler<AccountCreated>
{
    private readonly IReadModelRepository<AccountReadModel> _readModelRepository;
    
    public AccountReadModelUpdater(IReadModelRepository<AccountReadModel> readModelRepository)
    {
        _readModelRepository = readModelRepository;
    }
    
    public void Handle(AccountCreated @event)
    {
        // Create a new read model
        var readModel = new AccountReadModel
        {
            Id = @event.AccountId,
            AccountNumber = @event.AccountNumber,
            Balance = @event.InitialDeposit,
            IsActive = true,
            LastUpdated = DateTime.UtcNow
        };
        
        // Save the read model
        _readModelRepository.Save(readModel);
    }
}
```

### Correlated Event

```csharp
public class FundsDeposited : Event, ICorrelatedMessage
{
    public Guid AccountId { get; }
    public decimal Amount { get; }
    
    public Guid MsgId { get; }
    public Guid CorrelationId { get; }
    public Guid CausationId { get; }
    
    public FundsDeposited(
        Guid accountId, 
        decimal amount,
        Guid msgId,
        Guid correlationId,
        Guid causationId)
    {
        AccountId = accountId;
        Amount = amount;
        
        MsgId = msgId;
        CorrelationId = correlationId;
        CausationId = causationId;
    }
    
    // Alternative constructor using MessageBuilder
    public static FundsDeposited Create(
        Guid accountId,
        decimal amount,
        ICorrelatedMessage source)
    {
        return MessageBuilder.From(source, () => new FundsDeposited(
            accountId,
            amount,
            Guid.NewGuid(),
            source.CorrelationId,
            source.MsgId));
    }
}
```

## Event Bus Integration

Events are typically published through an event bus, which distributes them to their subscribers:

```csharp
// Create an event bus
var eventBus = new EventBus();

// Register event handlers
eventBus.Subscribe<AccountCreated>(evt => 
{
    // Update the read model
    var readModel = new AccountReadModel
    {
        Id = evt.AccountId,
        AccountNumber = evt.AccountNumber,
        Balance = evt.InitialDeposit,
        IsActive = true,
        LastUpdated = DateTime.UtcNow
    };
    
    readModelRepository.Save(readModel);
});

// Create and publish an event
var accountCreatedEvent = new AccountCreated(Guid.NewGuid(), "12345", 1000);
eventBus.Publish(accountCreatedEvent);
```

## Event Handling Patterns

### Read Model Updater

```csharp
public class AccountReadModelUpdater : 
    IEventHandler<AccountCreated>,
    IEventHandler<FundsDeposited>,
    IEventHandler<FundsWithdrawn>,
    IEventHandler<AccountClosed>
{
    private readonly IReadModelRepository<AccountReadModel> _readModelRepository;
    private readonly ILogger<AccountReadModelUpdater> _logger;
    
    public AccountReadModelUpdater(
        IReadModelRepository<AccountReadModel> readModelRepository,
        ILogger<AccountReadModelUpdater> logger)
    {
        _readModelRepository = readModelRepository;
        _logger = logger;
    }
    
    public void Handle(AccountCreated @event)
    {
        _logger.LogInformation("Handling AccountCreated event for account {@AccountId}", @event.AccountId);
        
        // Create a new read model
        var readModel = new AccountReadModel
        {
            Id = @event.AccountId,
            AccountNumber = @event.AccountNumber,
            Balance = @event.InitialDeposit,
            IsActive = true,
            LastUpdated = DateTime.UtcNow
        };
        
        // Save the read model
        _readModelRepository.Save(readModel);
    }
    
    public void Handle(FundsDeposited @event)
    {
        _logger.LogInformation("Handling FundsDeposited event for account {@AccountId}", @event.AccountId);
        
        // Get the read model
        var readModel = _readModelRepository.GetById(@event.AccountId);
        
        // Update the read model
        readModel.Balance += @event.Amount;
        readModel.LastUpdated = DateTime.UtcNow;
        
        // Save the read model
        _readModelRepository.Save(readModel);
    }
    
    public void Handle(FundsWithdrawn @event)
    {
        _logger.LogInformation("Handling FundsWithdrawn event for account {@AccountId}", @event.AccountId);
        
        // Get the read model
        var readModel = _readModelRepository.GetById(@event.AccountId);
        
        // Update the read model
        readModel.Balance -= @event.Amount;
        readModel.LastUpdated = DateTime.UtcNow;
        
        // Save the read model
        _readModelRepository.Save(readModel);
    }
    
    public void Handle(AccountClosed @event)
    {
        _logger.LogInformation("Handling AccountClosed event for account {@AccountId}", @event.AccountId);
        
        // Get the read model
        var readModel = _readModelRepository.GetById(@event.AccountId);
        
        // Update the read model
        readModel.IsActive = false;
        readModel.LastUpdated = DateTime.UtcNow;
        
        // Save the read model
        _readModelRepository.Save(readModel);
    }
}
```

### Process Manager

```csharp
public class AccountTransferProcessManager : 
    IEventHandler<TransferInitiated>,
    IEventHandler<FundsWithdrawn>,
    IEventHandler<FundsDeposited>,
    IEventHandler<TransferCompleted>,
    IEventHandler<TransferFailed>
{
    private readonly ICommandBus _commandBus;
    private readonly IRepository _repository;
    private readonly ILogger<AccountTransferProcessManager> _logger;
    
    public AccountTransferProcessManager(
        ICommandBus commandBus,
        IRepository repository,
        ILogger<AccountTransferProcessManager> logger)
    {
        _commandBus = commandBus;
        _repository = repository;
        _logger = logger;
    }
    
    public void Handle(TransferInitiated @event)
    {
        _logger.LogInformation("Handling TransferInitiated event for transfer {@TransferId}", @event.TransferId);
        
        // Create a withdraw command
        var withdrawCommand = MessageBuilder.From(@event, () => new WithdrawFunds(
            @event.SourceAccountId,
            @event.Amount,
            @event.TransferId));
            
        // Send the withdraw command
        _commandBus.Send(withdrawCommand);
    }
    
    public void Handle(FundsWithdrawn @event)
    {
        _logger.LogInformation("Handling FundsWithdrawn event for account {@AccountId}", @event.AccountId);
        
        // Get the transfer process
        var transfer = _repository.GetById<TransferProcess>(@event.CorrelationId);
        
        // Update the transfer process
        transfer.MarkFundsWithdrawn(@event);
        
        // Save the transfer process
        _repository.Save(transfer);
        
        // If this is part of a transfer, create a deposit command
        if (transfer.Status == TransferStatus.FundsWithdrawn)
        {
            var depositCommand = MessageBuilder.From(@event, () => new DepositFunds(
                transfer.TargetAccountId,
                transfer.Amount,
                transfer.Id));
                
            // Send the deposit command
            _commandBus.Send(depositCommand);
        }
    }
    
    // Additional handlers...
}
```

## Best Practices

1. **Event Naming**: Use past tense verbs for event names (e.g., `AccountCreated`, `FundsDeposited`)
2. **Event Immutability**: Make events immutable to prevent unintended side effects
3. **Event Properties**: Include all relevant information in event properties
4. **Event Versioning**: Plan for event versioning to handle schema evolution
5. **Event Handlers**: Design event handlers to be idempotent
6. **Multiple Subscribers**: Design for multiple subscribers to the same event
7. **Correlation**: Use correlation IDs to track event flows through the system
8. **Event Documentation**: Document the purpose and content of each event
9. **Event Serialization**: Ensure events can be properly serialized and deserialized
10. **Event Ordering**: Maintain the correct order of events when replaying

## Common Pitfalls

1. **Mutable Events**: Creating events that can be modified after creation
2. **Missing Properties**: Not including all necessary information in events
3. **Event Coupling**: Creating tight coupling between event publishers and subscribers
4. **Event Overloading**: Creating events that carry too much information
5. **Non-Idempotent Handlers**: Creating event handlers that produce different results when processing the same event multiple times
6. **Event Order Dependency**: Creating dependencies on event processing order that may not be guaranteed
7. **Missing Error Handling**: Not properly handling exceptions in event handlers
8. **Event Naming Inconsistency**: Inconsistent naming conventions for events

## Advanced Scenarios

### Event Upcasting

Handling event schema evolution through upcasting:

```csharp
public class EventUpcastingService : IEventUpcastingService
{
    public object Upcast(object @event)
    {
        // Check if the event needs upcasting
        if (@event is AccountCreatedV1 eventV1)
        {
            // Upcast from V1 to V2
            return new AccountCreatedV2(
                eventV1.AccountId,
                eventV1.AccountNumber,
                eventV1.InitialDeposit,
                DateTime.UtcNow); // Add new property
        }
        
        // Return the original event if no upcasting is needed
        return @event;
    }
}

// Original event (V1)
public class AccountCreatedV1 : Event
{
    public Guid AccountId { get; }
    public string AccountNumber { get; }
    public decimal InitialDeposit { get; }
    
    public AccountCreatedV1(Guid accountId, string accountNumber, decimal initialDeposit)
    {
        AccountId = accountId;
        AccountNumber = accountNumber;
        InitialDeposit = initialDeposit;
    }
}

// New version of the event (V2)
public class AccountCreatedV2 : Event
{
    public Guid AccountId { get; }
    public string AccountNumber { get; }
    public decimal InitialDeposit { get; }
    public DateTime CreatedAt { get; } // New property
    
    public AccountCreatedV2(Guid accountId, string accountNumber, decimal initialDeposit, DateTime createdAt)
    {
        AccountId = accountId;
        AccountNumber = accountNumber;
        InitialDeposit = initialDeposit;
        CreatedAt = createdAt;
    }
}
```

### Event Enrichment

Adding additional information to events before processing:

```csharp
public class EnrichingEventBus : IEventBus
{
    private readonly IEventBus _innerBus;
    private readonly IEventEnricher _enricher;
    
    public EnrichingEventBus(IEventBus innerBus, IEventEnricher enricher)
    {
        _innerBus = innerBus;
        _enricher = enricher;
    }
    
    public void Publish<T>(T @event) where T : class, IEvent
    {
        // Enrich the event
        var enrichedEvent = _enricher.Enrich(@event);
        
        // Publish the enriched event
        _innerBus.Publish(enrichedEvent);
    }
    
    // Implement other methods...
}

public class EventEnricher : IEventEnricher
{
    private readonly IUserContext _userContext;
    private readonly ITimeProvider _timeProvider;
    
    public EventEnricher(IUserContext userContext, ITimeProvider timeProvider)
    {
        _userContext = userContext;
        _timeProvider = timeProvider;
    }
    
    public T Enrich<T>(T @event) where T : class, IEvent
    {
        // If the event is enrichable, add additional information
        if (@event is IEnrichableEvent enrichableEvent)
        {
            enrichableEvent.UserId = _userContext.CurrentUser?.Id;
            enrichableEvent.Timestamp = _timeProvider.UtcNow;
        }
        
        return @event;
    }
}

public interface IEnrichableEvent
{
    Guid? UserId { get; set; }
    DateTime Timestamp { get; set; }
}
```

### Event Filtering

Filtering events before processing:

```csharp
public class FilteringEventBus : IEventBus
{
    private readonly IEventBus _innerBus;
    private readonly IEventFilter _filter;
    
    public FilteringEventBus(IEventBus innerBus, IEventFilter filter)
    {
        _innerBus = innerBus;
        _filter = filter;
    }
    
    public void Publish<T>(T @event) where T : class, IEvent
    {
        // Check if the event should be published
        if (_filter.ShouldPublish(@event))
        {
            // Publish the event
            _innerBus.Publish(@event);
        }
    }
    
    // Implement other methods...
}

public class EventFilter : IEventFilter
{
    private readonly IUserContext _userContext;
    
    public EventFilter(IUserContext userContext)
    {
        _userContext = userContext;
    }
    
    public bool ShouldPublish<T>(T @event) where T : class, IEvent
    {
        // Check if the event is restricted
        if (@event is IRestrictedEvent restrictedEvent)
        {
            // Check if the current user has access to the event
            return _userContext.CurrentUser?.HasAccess(restrictedEvent.AccessLevel) ?? false;
        }
        
        // By default, publish all events
        return true;
    }
}

public interface IRestrictedEvent
{
    string AccessLevel { get; }
}
```

## Related Components

- [Event](event.md): Base class for events in Reactive Domain
- [IEventBus](ievent-bus.md): Interface for publishing events
- [IEventHandler](ievent-handler.md): Interface for event handlers
- [IEventProcessor](ievent-processor.md): Interface for components that process events
- [IMessage](imessage.md): Base interface for all messages
- [ICorrelatedMessage](icorrelated-message.md): Interface for correlated messages
- [MessageBuilder](message-builder.md): Factory for creating correlated messages
- [AggregateRoot](aggregate-root.md): Base class for domain aggregates
- [ReadModelBase](read-model-base.md): Base class for read models

---

**Navigation**:
- [← Previous: ICommandHandler](./icommand-handler.md)
- [↑ Back to Top](#ievent-interface)
- [→ Next: Event](./event.md)
