# IEventBus Interface

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

## Overview

The `IEventBus` interface defines the contract for a component that publishes events to their subscribers in an event-driven architecture. It serves as a mediator between event publishers (typically aggregates or command handlers) and event subscribers (such as read model updaters, process managers, and other event handlers).

In Reactive Domain, the event bus is a fundamental component that enables loose coupling between components through event-based communication, supporting the event sourcing and CQRS architectural patterns.

## Event Bus in Event-Driven Architecture

In an event-driven architecture, events represent facts that have occurred in the system. The event bus is responsible for:

1. **Event Publishing**: Distributing events to all interested subscribers
2. **Event Subscription**: Allowing components to register interest in specific event types
3. **Decoupling**: Ensuring publishers and subscribers are not directly dependent on each other
4. **Correlation Tracking**: Maintaining correlation information across event flows
5. **Event Routing**: Directing events to the appropriate handlers based on event type

The event bus helps maintain a clean separation between components, allowing the system to evolve more easily as requirements change.

**Namespace**: `ReactiveDomain.Messaging`  
**Assembly**: `ReactiveDomain.Messaging.dll`

```csharp
public interface IEventBus
{
    void Publish<T>(T @event) where T : class, IEvent;
    void Subscribe<T>(Action<T> handler) where T : class, IEvent;
    void Unsubscribe<T>(Action<T> handler) where T : class, IEvent;
}
```

## Methods

### Publish\<T\>

Publishes an event to all registered subscribers.

```csharp
void Publish<T>(T @event) where T : class, IEvent;
```

**Type Parameters**:
- `T`: The type of event to publish. Must be a class that implements `IEvent`.

**Parameters**:
- `event` (`T`): The event to publish.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `event` is `null`.

**Remarks**: This method publishes an event to all registered subscribers. Unlike commands, which typically have a single handler, events can have multiple subscribers. The event bus ensures that the event is delivered to all interested subscribers.

**Example**:
```csharp
// Create an event
var accountCreatedEvent = new AccountCreated(Guid.NewGuid(), "12345", 1000);

// Publish the event to all subscribers
eventBus.Publish(accountCreatedEvent);
```

### Subscribe\<T\>

Subscribes a handler to a specific event type.

```csharp
void Subscribe<T>(Action<T> handler) where T : class, IEvent;
```

**Type Parameters**:
- `T`: The type of event to subscribe to. Must be a class that implements `IEvent`.

**Parameters**:
- `handler` (`System.Action<T>`): The handler to register for the event type.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `handler` is `null`.

**Remarks**: This method registers a handler for a specific event type. Multiple handlers can be registered for the same event type, allowing different components to react to the same event independently.

**Example**:
```csharp
// Subscribe a handler for the AccountCreated event
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
    
    // Save the read model
    readModelRepository.Save(readModel);
});
```

### Unsubscribe\<T\>

Unsubscribes a handler from a specific event type.

```csharp
void Unsubscribe<T>(Action<T> handler) where T : class, IEvent;
```

**Type Parameters**:
- `T`: The type of event to unsubscribe from. Must be a class that implements `IEvent`.

**Parameters**:
- `handler` (`System.Action<T>`): The handler to unregister for the event type.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `handler` is `null`.

**Remarks**: This method unregisters a handler for a specific event type. It is typically used when a component is being disposed or when dynamic handler registration is required.

**Example**:
```csharp
// Define a handler
Action<AccountCreated> accountCreatedHandler = evt => 
{
    // Handler logic
};

// Subscribe the handler
eventBus.Subscribe(accountCreatedHandler);

// Later, unsubscribe the handler
eventBus.Unsubscribe(accountCreatedHandler);
```

## Usage

The `IEventBus` interface is typically used to implement the event distribution mechanism in a CQRS/Event Sourcing architecture. Here's a comprehensive example of using an event bus:

### Basic Event Bus Usage

```csharp
// Create an event bus
var eventBus = new EventBus();

// Subscribe handlers
eventBus.Subscribe<AccountCreated>(HandleAccountCreated);
eventBus.Subscribe<FundsDeposited>(HandleFundsDeposited);
eventBus.Subscribe<FundsWithdrawn>(HandleFundsWithdrawn);
eventBus.Subscribe<AccountClosed>(HandleAccountClosed);

// Create and publish an event
var accountCreatedEvent = new AccountCreated(Guid.NewGuid(), "12345", 1000);
eventBus.Publish(accountCreatedEvent);

// Handler methods
void HandleAccountCreated(AccountCreated @event)
{
    // Update the read model
    var readModel = new AccountReadModel
    {
        Id = @event.AccountId,
        AccountNumber = @event.AccountNumber,
        Balance = @event.InitialDeposit,
        IsActive = true,
        LastUpdated = DateTime.UtcNow
    };
    
    // Save the read model
    readModelRepository.Save(readModel);
}

void HandleFundsDeposited(FundsDeposited @event)
{
    // Update the read model
    var readModel = readModelRepository.GetById(@event.AccountId);
    readModel.Balance += @event.Amount;
    readModel.LastUpdated = DateTime.UtcNow;
    
    // Save the read model
    readModelRepository.Save(readModel);
}

// Additional handlers...
```

### Integration with Dependency Injection

```csharp
public class EventHandlerRegistration : IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly IReadModelRepository<AccountReadModel> _readModelRepository;
    
    public EventHandlerRegistration(IEventBus eventBus, IReadModelRepository<AccountReadModel> readModelRepository)
    {
        _eventBus = eventBus;
        _readModelRepository = readModelRepository;
        
        // Register handlers
        RegisterHandlers();
    }
    
    private void RegisterHandlers()
    {
        _eventBus.Subscribe<AccountCreated>(HandleAccountCreated);
        _eventBus.Subscribe<FundsDeposited>(HandleFundsDeposited);
        _eventBus.Subscribe<FundsWithdrawn>(HandleFundsWithdrawn);
        _eventBus.Subscribe<AccountClosed>(HandleAccountClosed);
    }
    
    private void HandleAccountCreated(AccountCreated @event)
    {
        // Implementation...
    }
    
    // Additional handlers...
    
    public void Dispose()
    {
        // Unregister handlers
        _eventBus.Unsubscribe<AccountCreated>(HandleAccountCreated);
        _eventBus.Unsubscribe<FundsDeposited>(HandleFundsDeposited);
        _eventBus.Unsubscribe<FundsWithdrawn>(HandleFundsWithdrawn);
        _eventBus.Unsubscribe<AccountClosed>(HandleAccountClosed);
    }
}
```

### Multiple Subscribers for the Same Event

One of the key advantages of the event bus is that multiple subscribers can react to the same event:

```csharp
// Read model updater
eventBus.Subscribe<AccountCreated>(evt => 
{
    // Update the account read model
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

// Notification service
eventBus.Subscribe<AccountCreated>(evt => 
{
    // Send a welcome email
    emailService.SendWelcomeEmail(evt.AccountId, evt.AccountNumber);
});

// Audit logger
eventBus.Subscribe<AccountCreated>(evt => 
{
    // Log the event for audit purposes
    auditLogger.LogEvent("AccountCreated", evt);
});

// Analytics service
eventBus.Subscribe<AccountCreated>(evt => 
{
    // Track the event for analytics
    analyticsService.TrackEvent("AccountCreated", evt.AccountId);
});
```

### Integration with Command Bus and Repository

In a complete CQRS/Event Sourcing architecture, the event bus works together with the command bus and repository:

```csharp
public class AccountCommandHandler : 
    ICommandHandler<CreateAccount>,
    ICommandHandler<DepositFunds>,
    ICommandHandler<WithdrawFunds>,
    ICommandHandler<CloseAccount>
{
    private readonly IRepository _repository;
    private readonly IEventBus _eventBus;
    
    public AccountCommandHandler(IRepository repository, IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }
    
    public void Handle(CreateAccount command)
    {
        // Create a new account
        var account = new Account(command.AccountId);
        account.CreateAccount(command.AccountNumber, command.InitialDeposit, command);
        
        // Save the account
        _repository.Save(account);
        
        // Publish events
        foreach (var @event in account.TakeEvents())
        {
            _eventBus.Publish(@event);
        }
    }
    
    // Additional handlers...
}
```

## Best Practices

1. **Event Immutability**: Make events immutable to prevent unintended side effects
2. **Multiple Subscribers**: Design for multiple subscribers to the same event
3. **Event Naming**: Use past tense verbs for event names (e.g., `AccountCreated`, `FundsDeposited`)
4. **Event Properties**: Include all relevant information in events for subscribers
5. **Event Versioning**: Plan for event versioning to handle schema evolution
6. **Error Handling**: Implement proper error handling in event subscribers
7. **Idempotent Handlers**: Design event handlers to be idempotent
8. **Correlation Tracking**: Use correlated events to maintain traceability
9. **Event Logging**: Log events for auditing and debugging purposes
10. **Event Ordering**: Ensure events are processed in the correct order when necessary

## Common Pitfalls

1. **Event Coupling**: Creating tight coupling between event publishers and subscribers
2. **Missing Information**: Not including all necessary information in events
3. **Side Effects**: Performing side effects in event handlers that are not idempotent
4. **Event Overload**: Publishing too many events or events with too much information
5. **Synchronous Processing**: Blocking the event publisher while subscribers process events
6. **Missing Error Handling**: Not properly handling exceptions in event subscribers
7. **Event Bus Overuse**: Using the event bus for commands or queries
8. **Event Order Dependency**: Creating dependencies on event processing order

## Advanced Scenarios

### Asynchronous Event Processing

Implementing asynchronous event processing:

```csharp
public class AsyncEventBus : IEventBus
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();
    private readonly TaskFactory _taskFactory;
    
    public AsyncEventBus()
    {
        _taskFactory = new TaskFactory(new LimitedConcurrencyTaskScheduler(10));
    }
    
    public void Publish<T>(T @event) where T : class, IEvent
    {
        if (@event == null)
            throw new ArgumentNullException(nameof(@event));
            
        var eventType = typeof(T);
        
        if (!_handlers.TryGetValue(eventType, out var handlers))
            return;
            
        foreach (var handler in handlers.Cast<Action<T>>())
        {
            var localHandler = handler; // Capture for closure
            _taskFactory.StartNew(() => 
            {
                try
                {
                    localHandler(@event);
                }
                catch (Exception ex)
                {
                    // Log the exception
                    Console.WriteLine($"Error handling event {eventType.Name}: {ex.Message}");
                }
            });
        }
    }
    
    // Implement other methods...
}
```

### Event Upcasting

Handling event schema evolution through upcasting:

```csharp
public class UpcastingEventBus : IEventBus
{
    private readonly IEventBus _innerBus;
    private readonly IEventUpcastingService _upcastingService;
    
    public UpcastingEventBus(IEventBus innerBus, IEventUpcastingService upcastingService)
    {
        _innerBus = innerBus;
        _upcastingService = upcastingService;
    }
    
    public void Publish<T>(T @event) where T : class, IEvent
    {
        if (@event == null)
            throw new ArgumentNullException(nameof(@event));
            
        // Upcast the event if needed
        var upcastedEvent = _upcastingService.Upcast(@event);
        
        // Publish the upcasted event
        _innerBus.Publish(upcastedEvent);
    }
    
    // Implement other methods...
}
```

### Event Logging and Monitoring

Adding logging and monitoring to the event bus:

```csharp
public class MonitoringEventBus : IEventBus
{
    private readonly IEventBus _innerBus;
    private readonly ILogger _logger;
    private readonly IMetrics _metrics;
    
    public MonitoringEventBus(IEventBus innerBus, ILogger logger, IMetrics metrics)
    {
        _innerBus = innerBus;
        _logger = logger;
        _metrics = metrics;
    }
    
    public void Publish<T>(T @event) where T : class, IEvent
    {
        if (@event == null)
            throw new ArgumentNullException(nameof(@event));
            
        var eventType = typeof(T).Name;
        var correlationId = @event is ICorrelatedMessage msg ? msg.CorrelationId : Guid.Empty;
        
        _logger.LogInformation("Publishing event {EventType} with correlation ID {CorrelationId}", 
            eventType, correlationId);
            
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _innerBus.Publish(@event);
            
            stopwatch.Stop();
            _metrics.RecordEventProcessingTime(eventType, stopwatch.ElapsedMilliseconds);
            _metrics.IncrementEventCounter(eventType);
            
            _logger.LogInformation("Event {EventType} published successfully in {ElapsedMs}ms", 
                eventType, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.IncrementEventErrorCounter(eventType);
            
            _logger.LogError(ex, "Error publishing event {EventType}", eventType);
            throw;
        }
    }
    
    // Implement other methods...
}
```

## Related Components

- [Event](event.md): Base class for events in Reactive Domain
- [IEvent](ievent.md): Interface for events in Reactive Domain
- [ICorrelatedMessage](icorrelated-message.md): Interface for correlated messages
- [MessageBuilder](message-builder.md): Factory for creating correlated messages
- [ICommandBus](icommand-bus.md): Interface for sending commands
- [IEventHandler](ievent-handler.md): Interface for event handlers
- [ReadModelBase](read-model-base.md): Base class for read models

---

**Navigation**:
- [← Previous: ICommandBus](./icommand-bus.md)
- [↑ Back to Top](#ieventbus-interface)
- [→ Next: IEventHandler](./ievent-handler.md)
