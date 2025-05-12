# IEventProcessor Interface

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

## Overview

The `IEventProcessor` interface defines the contract for components that process events from an event store. It serves as a crucial component in event-sourced systems, enabling the consumption of events for various purposes such as updating read models, triggering processes, and maintaining system state.

In Reactive Domain, event processors are responsible for reading events from the event store, processing them in the correct order, and ensuring that each event is processed exactly once, even in the face of failures or restarts.

## Event Processing in Event Sourcing

In event-sourced systems, event processors play several critical roles:

1. **Event Consumption**: Reading events from the event store in the correct order
2. **Checkpoint Management**: Tracking which events have been processed to enable resumption after failures
3. **Idempotent Processing**: Ensuring events are processed exactly once
4. **Concurrency Management**: Handling concurrent event processing safely
5. **Error Handling**: Managing failures during event processing

Event processors are essential for maintaining the read side of CQRS architectures, where events from the write side are used to update read models that support queries and views.

**Namespace**: `ReactiveDomain.Messaging`  
**Assembly**: `ReactiveDomain.Messaging.dll`

```csharp
public interface IEventProcessor
{
    void Start();
    void Stop();
    void Subscribe<T>(Action<T> handler) where T : class, IEvent;
    void Unsubscribe<T>(Action<T> handler) where T : class, IEvent;
    long Position { get; }
}
```

## Properties

### Position

Gets the current position of the event processor in the event stream.

```csharp
long Position { get; }
```

**Returns**: `System.Int64` - The current position in the event stream.

**Remarks**: The position represents the sequence number of the last event that was successfully processed. This value is used to resume processing from the correct position after a restart.

## Methods

### Start

Starts the event processor, which begins consuming events from the event store.

```csharp
void Start();
```

**Remarks**: This method starts the event processor, which begins consuming events from the event store. If the processor has been previously stopped, it will resume from the last checkpoint position.

**Example**:
```csharp
// Create an event processor
var eventProcessor = new EventStoreProcessor(eventStoreConnection, "ReadModelUpdater");

// Subscribe handlers
eventProcessor.Subscribe<AccountCreated>(HandleAccountCreated);
eventProcessor.Subscribe<FundsDeposited>(HandleFundsDeposited);

// Start the processor
eventProcessor.Start();
```

### Stop

Stops the event processor, which ceases consuming events from the event store.

```csharp
void Stop();
```

**Remarks**: This method stops the event processor, which ceases consuming events from the event store. The current position is preserved, allowing the processor to resume from the same point when started again.

**Example**:
```csharp
// Stop the processor gracefully
eventProcessor.Stop();
```

### Subscribe\<T\>

Subscribes a handler to process events of a specific type.

```csharp
void Subscribe<T>(Action<T> handler) where T : class, IEvent;
```

**Type Parameters**:
- `T`: The type of event to subscribe to. Must be a class that implements `IEvent`.

**Parameters**:
- `handler` (`System.Action<T>`): The handler to process events of the specified type.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `handler` is `null`.

**Remarks**: This method registers a handler for a specific event type. When events of this type are read from the event store, they will be dispatched to the registered handler.

**Example**:
```csharp
// Subscribe a handler for AccountCreated events
eventProcessor.Subscribe<AccountCreated>(evt => 
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

Unsubscribes a handler from processing events of a specific type.

```csharp
void Unsubscribe<T>(Action<T> handler) where T : class, IEvent;
```

**Type Parameters**:
- `T`: The type of event to unsubscribe from. Must be a class that implements `IEvent`.

**Parameters**:
- `handler` (`System.Action<T>`): The handler to unregister.

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
eventProcessor.Subscribe(accountCreatedHandler);

// Later, unsubscribe the handler
eventProcessor.Unsubscribe(accountCreatedHandler);
```

## Usage

The `IEventProcessor` interface is typically used to implement components that update read models or trigger processes based on events from the event store. Here's a comprehensive example of using an event processor:

### Basic Event Processor Usage

```csharp
// Create an event processor
var connectionSettings = ConnectionSettings.Create()
    .SetDefaultUserCredentials(new UserCredentials("admin", "changeit"))
    .Build();
    
var eventStoreConnection = EventStoreConnection.Create(connectionSettings, new Uri("tcp://localhost:1113"));
eventStoreConnection.ConnectAsync().Wait();

var eventProcessor = new EventStoreProcessor(eventStoreConnection, "ReadModelUpdater");

// Subscribe handlers
eventProcessor.Subscribe<AccountCreated>(HandleAccountCreated);
eventProcessor.Subscribe<FundsDeposited>(HandleFundsDeposited);
eventProcessor.Subscribe<FundsWithdrawn>(HandleFundsWithdrawn);
eventProcessor.Subscribe<AccountClosed>(HandleAccountClosed);

// Start the processor
eventProcessor.Start();

// Handler methods
void HandleAccountCreated(AccountCreated @event)
{
    Console.WriteLine($"Processing AccountCreated event: {@event.AccountId}");
    
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
    Console.WriteLine($"Processing FundsDeposited event: {@event.AccountId}, Amount: {@event.Amount}");
    
    // Update the read model
    var readModel = readModelRepository.GetById(@event.AccountId);
    readModel.Balance += @event.Amount;
    readModel.LastUpdated = DateTime.UtcNow;
    
    // Save the read model
    readModelRepository.Save(readModel);
}

// Additional handlers...

// When shutting down
eventProcessor.Stop();
```

### Integration with Dependency Injection

```csharp
public class ReadModelUpdater : IDisposable
{
    private readonly IEventProcessor _eventProcessor;
    private readonly IReadModelRepository<AccountReadModel> _readModelRepository;
    private readonly ILogger<ReadModelUpdater> _logger;
    
    public ReadModelUpdater(
        IEventProcessor eventProcessor,
        IReadModelRepository<AccountReadModel> readModelRepository,
        ILogger<ReadModelUpdater> logger)
    {
        _eventProcessor = eventProcessor;
        _readModelRepository = readModelRepository;
        _logger = logger;
        
        // Register handlers
        RegisterHandlers();
    }
    
    private void RegisterHandlers()
    {
        _eventProcessor.Subscribe<AccountCreated>(HandleAccountCreated);
        _eventProcessor.Subscribe<FundsDeposited>(HandleFundsDeposited);
        _eventProcessor.Subscribe<FundsWithdrawn>(HandleFundsWithdrawn);
        _eventProcessor.Subscribe<AccountClosed>(HandleAccountClosed);
    }
    
    public void Start()
    {
        _logger.LogInformation("Starting read model updater");
        _eventProcessor.Start();
    }
    
    private void HandleAccountCreated(AccountCreated @event)
    {
        _logger.LogInformation("Processing AccountCreated event: {@AccountId}", @event.AccountId);
        
        // Implementation...
    }
    
    // Additional handlers...
    
    public void Dispose()
    {
        _logger.LogInformation("Stopping read model updater");
        _eventProcessor.Stop();
        
        // Unregister handlers
        _eventProcessor.Unsubscribe<AccountCreated>(HandleAccountCreated);
        _eventProcessor.Unsubscribe<FundsDeposited>(HandleFundsDeposited);
        _eventProcessor.Unsubscribe<FundsWithdrawn>(HandleFundsWithdrawn);
        _eventProcessor.Unsubscribe<AccountClosed>(HandleAccountClosed);
    }
}
```

### Multiple Event Processors

In a complex system, you might have multiple event processors for different purposes:

```csharp
// Read model updater
var readModelProcessor = new EventStoreProcessor(eventStoreConnection, "ReadModelUpdater");
readModelProcessor.Subscribe<AccountCreated>(evt => UpdateReadModel(evt));
readModelProcessor.Subscribe<FundsDeposited>(evt => UpdateReadModel(evt));
readModelProcessor.Start();

// Notification service
var notificationProcessor = new EventStoreProcessor(eventStoreConnection, "NotificationService");
notificationProcessor.Subscribe<AccountCreated>(evt => SendWelcomeEmail(evt));
notificationProcessor.Subscribe<FundsDeposited>(evt => SendDepositNotification(evt));
notificationProcessor.Start();

// Analytics service
var analyticsProcessor = new EventStoreProcessor(eventStoreConnection, "AnalyticsService");
analyticsProcessor.Subscribe<AccountCreated>(evt => TrackAccountCreation(evt));
analyticsProcessor.Subscribe<FundsDeposited>(evt => TrackDeposit(evt));
analyticsProcessor.Start();

// Process manager
var processManagerProcessor = new EventStoreProcessor(eventStoreConnection, "ProcessManager");
processManagerProcessor.Subscribe<AccountCreated>(evt => StartOnboardingProcess(evt));
processManagerProcessor.Subscribe<AccountClosed>(evt => StartOffboardingProcess(evt));
processManagerProcessor.Start();
```

## Best Practices

1. **Idempotent Handlers**: Design event handlers to be idempotent to handle duplicate events safely
2. **Error Handling**: Implement proper error handling in event handlers to prevent processor stalling
3. **Checkpoint Management**: Ensure checkpoints are properly persisted to enable resumption after failures
4. **Performance Tuning**: Configure batch sizes and polling intervals for optimal performance
5. **Monitoring**: Implement monitoring to track processor health and progress
6. **Logging**: Log event processing for debugging and auditing purposes
7. **Concurrency Control**: Configure the appropriate level of concurrency for event processing
8. **Graceful Shutdown**: Implement graceful shutdown procedures to prevent data loss
9. **Event Versioning**: Handle event schema evolution gracefully
10. **Resource Management**: Properly dispose of resources when shutting down

## Common Pitfalls

1. **Non-Idempotent Handlers**: Handlers that produce different results when processing the same event multiple times
2. **Unhandled Exceptions**: Exceptions in handlers that can cause the processor to stall
3. **Checkpoint Frequency**: Checkpointing too frequently (performance impact) or too infrequently (potential for duplicate processing)
4. **Event Order Dependency**: Creating dependencies on event processing order that may not be guaranteed
5. **Resource Leaks**: Not properly disposing of resources when shutting down
6. **Slow Handlers**: Handlers that take too long to process events, causing backpressure
7. **Missing Event Types**: Not handling all relevant event types
8. **Overloaded Processors**: Trying to do too much in a single processor, leading to performance issues

## Advanced Scenarios

### Custom Checkpoint Storage

Implementing custom checkpoint storage for an event processor:

```csharp
public class CustomCheckpointStore : ICheckpointStore
{
    private readonly IDocumentStore _documentStore;
    
    public CustomCheckpointStore(IDocumentStore documentStore)
    {
        _documentStore = documentStore;
    }
    
    public async Task<long> GetCheckpointAsync(string processorName)
    {
        using (var session = _documentStore.OpenSession())
        {
            var checkpoint = await session.LoadAsync<CheckpointDocument>(processorName);
            return checkpoint?.Position ?? -1;
        }
    }
    
    public async Task StoreCheckpointAsync(string processorName, long position)
    {
        using (var session = _documentStore.OpenSession())
        {
            var checkpoint = await session.LoadAsync<CheckpointDocument>(processorName) 
                ?? new CheckpointDocument { Id = processorName };
                
            checkpoint.Position = position;
            await session.StoreAsync(checkpoint);
            await session.SaveChangesAsync();
        }
    }
    
    private class CheckpointDocument
    {
        public string Id { get; set; }
        public long Position { get; set; }
    }
}
```

### Event Upcasting

Handling event schema evolution through upcasting:

```csharp
public class UpcastingEventProcessor : IEventProcessor
{
    private readonly IEventProcessor _innerProcessor;
    private readonly IEventUpcastingService _upcastingService;
    
    public UpcastingEventProcessor(IEventProcessor innerProcessor, IEventUpcastingService upcastingService)
    {
        _innerProcessor = innerProcessor;
        _upcastingService = upcastingService;
        
        // Subscribe to all events and upcast them before forwarding
        _innerProcessor.Subscribe<IEvent>(UpcasterHandler);
    }
    
    private void UpcasterHandler(IEvent @event)
    {
        // Upcast the event
        var upcastedEvent = _upcastingService.Upcast(@event);
        
        // Find the appropriate handler for the upcasted event
        var eventType = upcastedEvent.GetType();
        
        // Use reflection to find and invoke the appropriate handler
        // This is a simplified example; a real implementation would be more sophisticated
        if (_handlers.TryGetValue(eventType, out var handlers))
        {
            foreach (var handler in handlers)
            {
                handler.DynamicInvoke(upcastedEvent);
            }
        }
    }
    
    // Implement other methods...
}
```

### Parallel Event Processing

Implementing parallel event processing with careful ordering:

```csharp
public class ParallelEventProcessor : IEventProcessor
{
    private readonly IEventProcessor _innerProcessor;
    private readonly int _maxDegreeOfParallelism;
    private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();
    
    public ParallelEventProcessor(IEventProcessor innerProcessor, int maxDegreeOfParallelism)
    {
        _innerProcessor = innerProcessor;
        _maxDegreeOfParallelism = maxDegreeOfParallelism;
        
        // Subscribe to all events
        _innerProcessor.Subscribe<IEvent>(ParallelHandler);
    }
    
    private void ParallelHandler(IEvent @event)
    {
        var eventType = @event.GetType();
        
        if (_handlers.TryGetValue(eventType, out var handlers))
        {
            // Group events by aggregate ID to maintain ordering within an aggregate
            var aggregateId = GetAggregateId(@event);
            
            // Process handlers in parallel, but maintain order within each aggregate
            Parallel.ForEach(handlers, 
                new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism },
                handler => 
                {
                    try
                    {
                        handler.DynamicInvoke(@event);
                    }
                    catch (Exception ex)
                    {
                        // Log the exception
                        Console.WriteLine($"Error handling event {eventType.Name}: {ex.Message}");
                    }
                });
        }
    }
    
    private Guid GetAggregateId(IEvent @event)
    {
        // Extract the aggregate ID from the event
        // This is a simplified example; a real implementation would use reflection or a more sophisticated approach
        var property = @event.GetType().GetProperty("AggregateId") ?? 
                       @event.GetType().GetProperty("EntityId") ?? 
                       @event.GetType().GetProperty("Id");
                       
        return property != null ? (Guid)property.GetValue(@event) : Guid.Empty;
    }
    
    // Implement other methods...
}
```

## Related Components

- [Event](event.md): Base class for events in Reactive Domain
- [IEvent](ievent.md): Interface for events in Reactive Domain
- [IEventBus](ievent-bus.md): Interface for publishing events
- [IEventHandler](ievent-handler.md): Interface for event handlers
- [ReadModelBase](read-model-base.md): Base class for read models
- [IReadModelRepository](iread-model-repository.md): Interface for read model repositories
- [ICheckpointStore](icheckpoint-store.md): Interface for storing and retrieving checkpoints

---

**Navigation**:
- [← Previous: IEventBus](./ievent-bus.md)
- [↑ Back to Top](#ieventprocessor-interface)
- [→ Next: IEventHandler](./ievent-handler.md)
