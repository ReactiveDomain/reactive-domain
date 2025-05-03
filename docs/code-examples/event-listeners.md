# Setting Up Event Listeners

[← Back to Code Examples](README.md) | [← Back to Table of Contents](../README.md)

This example demonstrates how to set up event listeners in Reactive Domain to react to events as they occur.

## Event Listener Interface

```csharp
using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using MyApp.Domain;

namespace MyApp.EventHandlers
{
    public interface IEventListener<TEvent> : IHandleEvent<TEvent> where TEvent : IEvent
    {
        // This is a marker interface that extends IHandleEvent
    }
}
```

## Basic Event Listener

```csharp
using System;
using ReactiveDomain.Messaging;
using MyApp.Domain;

namespace MyApp.EventHandlers
{
    public class AccountEventLogger : 
        IEventListener<AccountCreated>,
        IEventListener<FundsDeposited>,
        IEventListener<FundsWithdrawn>,
        IEventListener<AccountClosed>
    {
        public void Handle(AccountCreated @event)
        {
            Console.WriteLine($"Account created: {@event.AccountId}, Number: {@event.AccountNumber}, Customer: {@event.CustomerName}");
            Console.WriteLine($"Correlation ID: {@event.CorrelationId}, Causation ID: {@event.CausationId}");
        }
        
        public void Handle(FundsDeposited @event)
        {
            Console.WriteLine($"Funds deposited: {@event.AccountId}, Amount: {@event.Amount}");
            Console.WriteLine($"Correlation ID: {@event.CorrelationId}, Causation ID: {@event.CausationId}");
        }
        
        public void Handle(FundsWithdrawn @event)
        {
            Console.WriteLine($"Funds withdrawn: {@event.AccountId}, Amount: {@event.Amount}");
            Console.WriteLine($"Correlation ID: {@event.CorrelationId}, Causation ID: {@event.CausationId}");
        }
        
        public void Handle(AccountClosed @event)
        {
            Console.WriteLine($"Account closed: {@event.AccountId}");
            Console.WriteLine($"Correlation ID: {@event.CorrelationId}, Causation ID: {@event.CausationId}");
        }
    }
}
```

## Read Model Event Listener

```csharp
using System;
using System.Collections.Generic;
using ReactiveDomain.Messaging;
using MyApp.Domain;
using MyApp.ReadModels;

namespace MyApp.EventHandlers
{
    public class AccountReadModelUpdater : 
        IEventListener<AccountCreated>,
        IEventListener<FundsDeposited>,
        IEventListener<FundsWithdrawn>,
        IEventListener<AccountClosed>
    {
        private readonly IReadModelRepository<AccountSummary> _repository;
        
        public AccountReadModelUpdater(IReadModelRepository<AccountSummary> repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }
        
        public void Handle(AccountCreated @event)
        {
            var accountSummary = new AccountSummary(@event.AccountId);
            accountSummary.Update(@event.AccountNumber, @event.CustomerName, 0, false);
            
            _repository.Save(accountSummary);
        }
        
        public void Handle(FundsDeposited @event)
        {
            var accountSummary = _repository.GetById(@event.AccountId);
            if (accountSummary != null)
            {
                accountSummary.UpdateBalance(accountSummary.Balance + @event.Amount);
                _repository.Save(accountSummary);
            }
        }
        
        public void Handle(FundsWithdrawn @event)
        {
            var accountSummary = _repository.GetById(@event.AccountId);
            if (accountSummary != null)
            {
                accountSummary.UpdateBalance(accountSummary.Balance - @event.Amount);
                _repository.Save(accountSummary);
            }
        }
        
        public void Handle(AccountClosed @event)
        {
            var accountSummary = _repository.GetById(@event.AccountId);
            if (accountSummary != null)
            {
                accountSummary.MarkAsClosed();
                _repository.Save(accountSummary);
            }
        }
    }
}
```

## Integration Event Publisher

```csharp
using System;
using System.Threading.Tasks;
using ReactiveDomain.Messaging;
using MyApp.Domain;
using MyApp.Integration;

namespace MyApp.EventHandlers
{
    public class IntegrationEventPublisher : 
        IEventListener<AccountCreated>,
        IEventListener<AccountClosed>
    {
        private readonly IIntegrationEventBus _eventBus;
        
        public IntegrationEventPublisher(IIntegrationEventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }
        
        public void Handle(AccountCreated @event)
        {
            // Create an integration event
            var integrationEvent = new AccountCreatedIntegrationEvent(
                @event.AccountId,
                @event.AccountNumber,
                @event.CustomerName,
                DateTime.UtcNow,
                @event.CorrelationId,
                @event.CausationId);
                
            // Publish the integration event
            _eventBus.PublishAsync(integrationEvent);
        }
        
        public void Handle(AccountClosed @event)
        {
            // Create an integration event
            var integrationEvent = new AccountClosedIntegrationEvent(
                @event.AccountId,
                DateTime.UtcNow,
                @event.CorrelationId,
                @event.CausationId);
                
            // Publish the integration event
            _eventBus.PublishAsync(integrationEvent);
        }
    }
    
    // Integration event bus interface
    public interface IIntegrationEventBus
    {
        Task PublishAsync<T>(T @event) where T : IntegrationEvent;
    }
    
    // Base integration event
    public abstract class IntegrationEvent
    {
        public Guid Id { get; }
        public DateTime Timestamp { get; }
        public Guid CorrelationId { get; }
        public Guid CausationId { get; }
        
        protected IntegrationEvent(DateTime timestamp, Guid correlationId, Guid causationId)
        {
            Id = Guid.NewGuid();
            Timestamp = timestamp;
            CorrelationId = correlationId;
            CausationId = causationId;
        }
    }
    
    // Integration events
    public class AccountCreatedIntegrationEvent : IntegrationEvent
    {
        public Guid AccountId { get; }
        public string AccountNumber { get; }
        public string CustomerName { get; }
        
        public AccountCreatedIntegrationEvent(
            Guid accountId,
            string accountNumber,
            string customerName,
            DateTime timestamp,
            Guid correlationId,
            Guid causationId)
            : base(timestamp, correlationId, causationId)
        {
            AccountId = accountId;
            AccountNumber = accountNumber;
            CustomerName = customerName;
        }
    }
    
    public class AccountClosedIntegrationEvent : IntegrationEvent
    {
        public Guid AccountId { get; }
        
        public AccountClosedIntegrationEvent(
            Guid accountId,
            DateTime timestamp,
            Guid correlationId,
            Guid causationId)
            : base(timestamp, correlationId, causationId)
        {
            AccountId = accountId;
        }
    }
}
```

## Registering Event Listeners

```csharp
using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using MyApp.Domain;
using MyApp.EventHandlers;
using MyApp.ReadModels;
using MyApp.Integration;

namespace MyApp.Infrastructure
{
    public class EventBusSetup
    {
        public IEventBus ConfigureEventBus(
            IReadModelRepository<AccountSummary> readModelRepository,
            IIntegrationEventBus integrationEventBus)
        {
            // Create an event bus
            var eventBus = new EventBus();
            
            // Create event listeners
            var accountEventLogger = new AccountEventLogger();
            var accountReadModelUpdater = new AccountReadModelUpdater(readModelRepository);
            var integrationEventPublisher = new IntegrationEventPublisher(integrationEventBus);
            
            // Register event listeners
            RegisterEventListeners<AccountCreated>(eventBus, 
                accountEventLogger, 
                accountReadModelUpdater, 
                integrationEventPublisher);
                
            RegisterEventListeners<FundsDeposited>(eventBus, 
                accountEventLogger, 
                accountReadModelUpdater);
                
            RegisterEventListeners<FundsWithdrawn>(eventBus, 
                accountEventLogger, 
                accountReadModelUpdater);
                
            RegisterEventListeners<AccountClosed>(eventBus, 
                accountEventLogger, 
                accountReadModelUpdater, 
                integrationEventPublisher);
                
            return eventBus;
        }
        
        private void RegisterEventListeners<TEvent>(
            IEventBus eventBus, 
            params IEventListener<TEvent>[] listeners) 
            where TEvent : IEvent
        {
            foreach (var listener in listeners)
            {
                eventBus.Subscribe(listener);
            }
        }
    }
}
```

## Connecting to Event Store Subscription

```csharp
using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.EventStore;
using ReactiveDomain.Persistence;
using MyApp.Domain;

namespace MyApp.Infrastructure
{
    public class EventStoreSubscription
    {
        private readonly IStreamStoreConnection _connection;
        private readonly IEventBus _eventBus;
        private readonly IEventSerializer _serializer;
        
        public EventStoreSubscription(
            IStreamStoreConnection connection,
            IEventBus eventBus,
            IEventSerializer serializer)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }
        
        public void SubscribeToAll()
        {
            // Subscribe to all events
            _connection.SubscribeToAll(
                eventAppeared: (subscription, resolvedEvent) =>
                {
                    try
                    {
                        // Deserialize the event
                        var @event = _serializer.Deserialize(resolvedEvent.Event);
                        
                        // Publish the event to the event bus
                        if (@event != null)
                        {
                            _eventBus.Publish(@event);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing event: {ex.Message}");
                    }
                },
                subscriptionDropped: (subscription, reason, exception) =>
                {
                    Console.WriteLine($"Subscription dropped: {reason}");
                    
                    // Reconnect after a delay
                    System.Threading.Thread.Sleep(1000);
                    SubscribeToAll();
                });
                
            Console.WriteLine("Subscribed to all events");
        }
        
        public void SubscribeToStream(string streamName)
        {
            // Subscribe to a specific stream
            _connection.SubscribeToStream(
                streamName,
                eventAppeared: (subscription, resolvedEvent) =>
                {
                    try
                    {
                        // Deserialize the event
                        var @event = _serializer.Deserialize(resolvedEvent.Event);
                        
                        // Publish the event to the event bus
                        if (@event != null)
                        {
                            _eventBus.Publish(@event);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing event: {ex.Message}");
                    }
                },
                subscriptionDropped: (subscription, reason, exception) =>
                {
                    Console.WriteLine($"Subscription dropped: {reason}");
                    
                    // Reconnect after a delay
                    System.Threading.Thread.Sleep(1000);
                    SubscribeToStream(streamName);
                });
                
            Console.WriteLine($"Subscribed to stream: {streamName}");
        }
    }
}
```

## Complete Example

```csharp
using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.EventStore;
using ReactiveDomain.Persistence;
using MyApp.Domain;
using MyApp.EventHandlers;
using MyApp.ReadModels;
using MyApp.Integration;
using MyApp.Infrastructure;

namespace MyApp.Examples
{
    public class EventListenerExample
    {
        public void SetupEventListeners()
        {
            // Create dependencies
            var readModelRepository = new InMemoryReadModelRepository<AccountSummary>();
            var integrationEventBus = new RabbitMqIntegrationEventBus("amqp://localhost");
            
            // Set up event bus with listeners
            var eventBusSetup = new EventBusSetup();
            var eventBus = eventBusSetup.ConfigureEventBus(readModelRepository, integrationEventBus);
            
            // Configure repository
            var repositoryConfig = new RepositoryConfiguration();
            var repository = repositoryConfig.ConfigureRepository("localhost");
            
            // Set up event store subscription
            var connection = repository.GetType()
                .GetProperty("Connection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(repository) as IStreamStoreConnection;
                
            var serializer = repository.GetType()
                .GetProperty("Serializer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(repository) as IEventSerializer;
                
            var subscription = new EventStoreSubscription(connection, eventBus, serializer);
            
            // Subscribe to all events
            subscription.SubscribeToAll();
            
            // Create and save an account to trigger events
            var accountId = Guid.NewGuid();
            var account = new Account(accountId);
            account.Create("ACC-123", "John Doe");
            repository.Save(account);
            
            // Deposit funds to trigger more events
            account.Deposit(1000);
            repository.Save(account);
            
            // Wait for events to be processed
            System.Threading.Thread.Sleep(1000);
            
            // Check the read model
            var accountSummary = readModelRepository.GetById(accountId);
            Console.WriteLine($"Read model: Account {accountSummary.Id}, Balance: {accountSummary.Balance}");
        }
    }
    
    // Simple in-memory read model repository for the example
    public class InMemoryReadModelRepository<T> : IReadModelRepository<T> where T : ReadModelBase
    {
        private readonly Dictionary<Guid, T> _items = new Dictionary<Guid, T>();
        
        public T GetById(Guid id)
        {
            if (_items.TryGetValue(id, out var item))
            {
                return item;
            }
            
            return null;
        }
        
        public void Save(T item)
        {
            _items[item.Id] = item;
        }
    }
    
    // Simple RabbitMQ integration event bus for the example
    public class RabbitMqIntegrationEventBus : IIntegrationEventBus
    {
        private readonly string _connectionString;
        
        public RabbitMqIntegrationEventBus(string connectionString)
        {
            _connectionString = connectionString;
        }
        
        public Task PublishAsync<T>(T @event) where T : IntegrationEvent
        {
            Console.WriteLine($"Publishing integration event: {@event.GetType().Name}");
            // In a real implementation, this would publish to RabbitMQ
            return Task.CompletedTask;
        }
    }
}
```

## Key Concepts

### Event Listeners

- Event listeners implement the `IHandleEvent<T>` interface
- They react to events as they occur in the system
- Multiple listeners can handle the same event for different purposes

### Types of Event Listeners

- **Logging Listeners**: Record events for auditing and debugging
- **Read Model Updaters**: Update read models for querying
- **Integration Event Publishers**: Publish events to external systems
- **Process Managers**: Coordinate complex workflows across multiple aggregates

### Event Bus

- The event bus routes events to their handlers
- Handlers are registered with the bus using the `Subscribe` method
- Events are published to the bus using the `Publish` method

### Event Store Subscription

- Subscribes to events from the event store
- Can subscribe to all events or specific streams
- Deserializes events and publishes them to the event bus
- Handles reconnection if the subscription is dropped

## Best Practices

1. **Single Responsibility**: Each event listener should have a single responsibility
2. **Error Handling**: Implement proper error handling in event listeners
3. **Idempotency**: Design event handlers to be idempotent (can be applied multiple times without changing the result)
4. **Asynchronous Processing**: Process events asynchronously to avoid blocking
5. **Subscription Management**: Handle subscription drops and reconnect automatically
6. **Correlation Tracking**: Maintain correlation information in integration events

## Common Pitfalls

1. **Event Handler Exceptions**: Unhandled exceptions in event handlers can break the subscription
2. **Missing Event Handlers**: Ensure all event types have appropriate handlers
3. **Tight Coupling**: Avoid tight coupling between event handlers and domain logic
4. **Performance Issues**: Be mindful of performance in event handlers, especially for high-volume events
5. **Lost Events**: Ensure proper error handling and retry mechanisms to avoid losing events

---

**Navigation**:
- [← Previous: Saving and Retrieving Aggregates](saving-retrieving-aggregates.md)
- [↑ Back to Top](#setting-up-event-listeners)
- [→ Next: Implementing Projections](implementing-projections.md)
