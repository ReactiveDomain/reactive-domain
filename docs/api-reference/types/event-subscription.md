# Event Subscription

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

Event subscription is a critical component of event-driven architectures in Reactive Domain. This document outlines the patterns and best practices for subscribing to and processing events, particularly for updating read models in a CQRS architecture.

## Overview

In event-driven systems, components need to react to events as they occur. Event subscription provides the mechanism for components to register interest in specific events and receive notifications when those events occur. This is particularly important in CQRS architectures, where read models need to be updated based on domain events.

## Subscription Patterns

### 1. Direct Event Handler Registration

The simplest approach is to register event handlers directly with an event bus or dispatcher.

```csharp
// Event handler implementation
public class AccountSummaryProjection : 
    IEventHandler<AccountCreated>,
    IEventHandler<FundsDeposited>,
    IEventHandler<FundsWithdrawn>
{
    private readonly IReadModelRepository<AccountSummary> _repository;
    
    public AccountSummaryProjection(IReadModelRepository<AccountSummary> repository)
    {
        _repository = repository;
    }
    
    public void Handle(AccountCreated @event)
    {
        // Update read model
    }
    
    public void Handle(FundsDeposited @event)
    {
        // Update read model
    }
    
    public void Handle(FundsWithdrawn @event)
    {
        // Update read model
    }
}

// Registration in startup
public void ConfigureServices(IServiceCollection services)
{
    // Register the projection as a handler for specific events
    services.AddScoped<IEventHandler<AccountCreated>, AccountSummaryProjection>();
    services.AddScoped<IEventHandler<FundsDeposited>, AccountSummaryProjection>();
    services.AddScoped<IEventHandler<FundsWithdrawn>, AccountSummaryProjection>();
}
```

### 2. Subscription Manager

A more flexible approach uses a subscription manager to dynamically register and manage event subscriptions.

```csharp
public interface IEventSubscriptionManager
{
    void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent;
    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent;
    void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent;
    void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent;
}

public class EventSubscriptionManager : IEventSubscriptionManager
{
    private readonly Dictionary<Type, List<object>> _subscriptions = new Dictionary<Type, List<object>>();
    
    public void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent
    {
        var eventType = typeof(TEvent);
        
        if (!_subscriptions.ContainsKey(eventType))
        {
            _subscriptions[eventType] = new List<object>();
        }
        
        _subscriptions[eventType].Add(handler);
    }
    
    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent
    {
        var eventType = typeof(TEvent);
        
        if (!_subscriptions.ContainsKey(eventType))
        {
            _subscriptions[eventType] = new List<object>();
        }
        
        _subscriptions[eventType].Add(handler);
    }
    
    public void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent
    {
        var eventType = typeof(TEvent);
        
        if (_subscriptions.ContainsKey(eventType))
        {
            _subscriptions[eventType].Remove(handler);
        }
    }
    
    public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent
    {
        var eventType = typeof(TEvent);
        
        if (_subscriptions.ContainsKey(eventType))
        {
            _subscriptions[eventType].Remove(handler);
        }
    }
    
    public IEnumerable<object> GetSubscriptionsForEvent<TEvent>() where TEvent : IEvent
    {
        var eventType = typeof(TEvent);
        
        if (_subscriptions.ContainsKey(eventType))
        {
            return _subscriptions[eventType];
        }
        
        return new List<object>();
    }
}
```

### 3. Event Processor with Catch-Up Capability

For more robust systems, an event processor that can catch up on missed events is essential.

```csharp
public interface IEventProcessor
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task SubscribeAsync<TEvent>(
        Func<TEvent, Task> handler, 
        CancellationToken cancellationToken = default) 
        where TEvent : IEvent;
}

public class EventStoreEventProcessor : IEventProcessor, IDisposable
{
    private readonly IEventStoreConnection _connection;
    private readonly ILogger<EventStoreEventProcessor> _logger;
    private readonly Dictionary<string, EventStoreCatchUpSubscription> _subscriptions;
    private readonly IEventSerializer _serializer;
    private bool _disposed;
    
    public EventStoreEventProcessor(
        IEventStoreConnection connection,
        IEventSerializer serializer,
        ILogger<EventStoreEventProcessor> logger)
    {
        _connection = connection;
        _serializer = serializer;
        _logger = logger;
        _subscriptions = new Dictionary<string, EventStoreCatchUpSubscription>();
    }
    
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _connection.ConnectAsync();
            _logger.LogInformation("Connected to Event Store");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Event Store");
            throw;
        }
    }
    
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        foreach (var subscription in _subscriptions.Values)
        {
            subscription.Stop();
        }
        
        _subscriptions.Clear();
        _logger.LogInformation("Stopped all Event Store subscriptions");
        
        return Task.CompletedTask;
    }
    
    public Task SubscribeAsync<TEvent>(
        Func<TEvent, Task> handler, 
        CancellationToken cancellationToken = default) 
        where TEvent : IEvent
    {
        var eventType = typeof(TEvent).Name;
        var streamName = $"$ce-{eventType}";
        
        if (_subscriptions.ContainsKey(streamName))
        {
            _logger.LogWarning("Already subscribed to event type {EventType}", eventType);
            return Task.CompletedTask;
        }
        
        var settings = new CatchUpSubscriptionSettings(
            maxLiveQueueSize: 10000,
            readBatchSize: 500,
            verboseLogging: false,
            resolveLinkTos: true,
            subscriptionName: $"Projection-{eventType}");
        
        _subscriptions[streamName] = _connection.SubscribeToStreamFrom(
            streamName,
            StreamCheckpoint.StreamStart,
            settings,
            eventAppeared: async (subscription, resolvedEvent, cancellationToken) =>
            {
                try
                {
                    if (resolvedEvent.Event.EventType != eventType)
                        return;
                        
                    var eventData = _serializer.Deserialize<TEvent>(
                        resolvedEvent.Event.Data);
                        
                    await handler(eventData);
                    
                    _logger.LogDebug(
                        "Processed event {EventType} with ID {EventId}",
                        eventType,
                        resolvedEvent.Event.EventId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error processing event {EventType} with ID {EventId}",
                        eventType,
                        resolvedEvent.Event.EventId);
                }
            },
            liveProcessingStarted: subscription =>
            {
                _logger.LogInformation(
                    "Caught up and processing live events for {EventType}",
                    eventType);
            },
            subscriptionDropped: (subscription, reason, exception) =>
            {
                _logger.LogWarning(
                    exception,
                    "Subscription dropped for {EventType}: {Reason}",
                    eventType,
                    reason);
                    
                // Attempt to reconnect after a delay
                Task.Delay(TimeSpan.FromSeconds(5))
                    .ContinueWith(_ => SubscribeAsync(handler, cancellationToken));
            });
            
        _logger.LogInformation("Subscribed to event type {EventType}", eventType);
        return Task.CompletedTask;
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;
            
        if (disposing)
        {
            foreach (var subscription in _subscriptions.Values)
            {
                subscription.Stop();
            }
            
            _subscriptions.Clear();
            _connection?.Dispose();
        }
        
        _disposed = true;
    }
}
```

### 4. Projection Manager

A projection manager coordinates multiple projections and their event subscriptions.

```csharp
public interface IProjectionManager
{
    Task StartAllProjectionsAsync(CancellationToken cancellationToken = default);
    Task StopAllProjectionsAsync(CancellationToken cancellationToken = default);
    Task RegisterProjectionAsync<TProjection>(TProjection projection) where TProjection : class;
}

public class ProjectionManager : IProjectionManager
{
    private readonly IEventProcessor _eventProcessor;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProjectionManager> _logger;
    private readonly List<object> _registeredProjections = new List<object>();
    
    public ProjectionManager(
        IEventProcessor eventProcessor,
        IServiceProvider serviceProvider,
        ILogger<ProjectionManager> logger)
    {
        _eventProcessor = eventProcessor;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    public async Task StartAllProjectionsAsync(CancellationToken cancellationToken = default)
    {
        await _eventProcessor.StartAsync(cancellationToken);
        
        foreach (var projection in _registeredProjections)
        {
            await RegisterEventHandlersAsync(projection, cancellationToken);
        }
        
        _logger.LogInformation("Started all projections");
    }
    
    public async Task StopAllProjectionsAsync(CancellationToken cancellationToken = default)
    {
        await _eventProcessor.StopAsync(cancellationToken);
        _logger.LogInformation("Stopped all projections");
    }
    
    public async Task RegisterProjectionAsync<TProjection>(TProjection projection) 
        where TProjection : class
    {
        _registeredProjections.Add(projection);
        
        // If the event processor is already running, register the handlers immediately
        await RegisterEventHandlersAsync(projection, CancellationToken.None);
        
        _logger.LogInformation(
            "Registered projection of type {ProjectionType}",
            typeof(TProjection).Name);
    }
    
    private async Task RegisterEventHandlersAsync(
        object projection, 
        CancellationToken cancellationToken)
    {
        var projectionType = projection.GetType();
        
        // Find all event handler interfaces implemented by the projection
        var handlerInterfaces = projectionType
            .GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>));
            
        foreach (var handlerInterface in handlerInterfaces)
        {
            var eventType = handlerInterface.GetGenericArguments()[0];
            var handleMethod = handlerInterface.GetMethod("Handle");
            
            if (handleMethod == null)
                continue;
                
            // Create a generic method to subscribe to the event
            var subscribeMethod = typeof(ProjectionManager)
                .GetMethod(nameof(SubscribeToEvent), BindingFlags.NonPublic | BindingFlags.Instance)
                .MakeGenericMethod(eventType);
                
            await (Task)subscribeMethod.Invoke(
                this, 
                new[] { projection, handleMethod, cancellationToken });
        }
    }
    
    private async Task SubscribeToEvent<TEvent>(
        object projection, 
        MethodInfo handleMethod,
        CancellationToken cancellationToken) 
        where TEvent : IEvent
    {
        await _eventProcessor.SubscribeAsync<TEvent>(
            async @event =>
            {
                try
                {
                    // Convert synchronous handlers to async if needed
                    var result = handleMethod.Invoke(projection, new object[] { @event });
                    
                    if (result is Task task)
                    {
                        await task;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error handling event {EventType} in projection {ProjectionType}",
                        typeof(TEvent).Name,
                        projection.GetType().Name);
                }
            },
            cancellationToken);
    }
}
```

## Subscription Lifecycle Management

### Starting Subscriptions on Application Startup

```csharp
public class ProjectionHostedService : IHostedService
{
    private readonly IProjectionManager _projectionManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProjectionHostedService> _logger;
    
    public ProjectionHostedService(
        IProjectionManager projectionManager,
        IServiceProvider serviceProvider,
        ILogger<ProjectionHostedService> logger)
    {
        _projectionManager = projectionManager;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting projection hosted service");
        
        // Register all projections
        RegisterProjections();
        
        // Start all projections
        await _projectionManager.StartAllProjectionsAsync(cancellationToken);
        
        _logger.LogInformation("Projection hosted service started");
    }
    
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping projection hosted service");
        
        await _projectionManager.StopAllProjectionsAsync(cancellationToken);
        
        _logger.LogInformation("Projection hosted service stopped");
    }
    
    private void RegisterProjections()
    {
        // Register all projections from the service provider
        var accountProjection = _serviceProvider.GetRequiredService<AccountSummaryProjection>();
        _projectionManager.RegisterProjectionAsync(accountProjection).GetAwaiter().GetResult();
        
        var transactionProjection = _serviceProvider.GetRequiredService<TransactionHistoryProjection>();
        _projectionManager.RegisterProjectionAsync(transactionProjection).GetAwaiter().GetResult();
        
        // Register other projections as needed
    }
}

// In Startup.cs or Program.cs
public void ConfigureServices(IServiceCollection services)
{
    // Register event processor and projection manager
    services.AddSingleton<IEventProcessor, EventStoreEventProcessor>();
    services.AddSingleton<IProjectionManager, ProjectionManager>();
    
    // Register projections
    services.AddScoped<AccountSummaryProjection>();
    services.AddScoped<TransactionHistoryProjection>();
    
    // Register hosted service to manage projection lifecycle
    services.AddHostedService<ProjectionHostedService>();
}
```

## Handling Subscription Failures

Robust event subscription systems need to handle various failure scenarios:

### 1. Connection Failures

```csharp
public class ResilientEventProcessor : IEventProcessor
{
    private readonly IEventProcessor _innerProcessor;
    private readonly ILogger<ResilientEventProcessor> _logger;
    private readonly int _maxRetries;
    private readonly TimeSpan _initialRetryDelay;
    
    public ResilientEventProcessor(
        IEventProcessor innerProcessor,
        ILogger<ResilientEventProcessor> logger,
        int maxRetries = 5,
        TimeSpan? initialRetryDelay = null)
    {
        _innerProcessor = innerProcessor;
        _logger = logger;
        _maxRetries = maxRetries;
        _initialRetryDelay = initialRetryDelay ?? TimeSpan.FromSeconds(1);
    }
    
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        int retryCount = 0;
        TimeSpan delay = _initialRetryDelay;
        
        while (true)
        {
            try
            {
                await _innerProcessor.StartAsync(cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                retryCount++;
                
                if (retryCount > _maxRetries)
                {
                    _logger.LogError(
                        ex,
                        "Failed to start event processor after {RetryCount} attempts",
                        retryCount);
                    throw;
                }
                
                _logger.LogWarning(
                    ex,
                    "Failed to start event processor, retrying in {Delay}ms (attempt {RetryCount}/{MaxRetries})",
                    delay.TotalMilliseconds,
                    retryCount,
                    _maxRetries);
                    
                await Task.Delay(delay, cancellationToken);
                
                // Exponential backoff
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
            }
        }
    }
    
    // Implement other methods with similar resilience patterns
}
```

### 2. Event Processing Failures

```csharp
// In the event processor
private async Task ProcessEventAsync<TEvent>(
    TEvent @event,
    Func<TEvent, Task> handler,
    string eventId)
    where TEvent : IEvent
{
    int retryCount = 0;
    TimeSpan delay = TimeSpan.FromMilliseconds(500);
    
    while (true)
    {
        try
        {
            await handler(@event);
            return;
        }
        catch (Exception ex)
        {
            retryCount++;
            
            if (retryCount > 3)
            {
                _logger.LogError(
                    ex,
                    "Failed to process event {EventType} with ID {EventId} after {RetryCount} attempts",
                    typeof(TEvent).Name,
                    eventId,
                    retryCount);
                    
                // Consider sending to a dead letter queue or error stream
                await SendToDeadLetterQueueAsync(@event, ex);
                return;
            }
            
            _logger.LogWarning(
                ex,
                "Failed to process event {EventType} with ID {EventId}, retrying in {Delay}ms (attempt {RetryCount}/3)",
                typeof(TEvent).Name,
                eventId,
                delay.TotalMilliseconds,
                retryCount);
                
            await Task.Delay(delay);
            
            // Exponential backoff
            delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
        }
    }
}

private async Task SendToDeadLetterQueueAsync<TEvent>(TEvent @event, Exception exception)
    where TEvent : IEvent
{
    try
    {
        var deadLetterEvent = new DeadLetterEvent<TEvent>
        {
            OriginalEvent = @event,
            ErrorMessage = exception.Message,
            StackTrace = exception.StackTrace,
            Timestamp = DateTime.UtcNow
        };
        
        // Store in a dead letter queue for later inspection or retry
        await _deadLetterRepository.SaveAsync(deadLetterEvent);
        
        _logger.LogInformation(
            "Sent event {EventType} to dead letter queue",
            typeof(TEvent).Name);
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Failed to send event {EventType} to dead letter queue",
            typeof(TEvent).Name);
    }
}
```

## Monitoring and Observability

Effective monitoring is essential for event subscription systems:

```csharp
public class MonitoredEventProcessor : IEventProcessor
{
    private readonly IEventProcessor _innerProcessor;
    private readonly IMetricsCollector _metrics;
    private readonly ILogger<MonitoredEventProcessor> _logger;
    
    public MonitoredEventProcessor(
        IEventProcessor innerProcessor,
        IMetricsCollector metrics,
        ILogger<MonitoredEventProcessor> logger)
    {
        _innerProcessor = innerProcessor;
        _metrics = metrics;
        _logger = logger;
    }
    
    public async Task SubscribeAsync<TEvent>(
        Func<TEvent, Task> handler, 
        CancellationToken cancellationToken = default) 
        where TEvent : IEvent
    {
        var eventType = typeof(TEvent).Name;
        
        await _innerProcessor.SubscribeAsync<TEvent>(
            async @event =>
            {
                var stopwatch = Stopwatch.StartNew();
                bool success = false;
                
                try
                {
                    using var activity = Activity.Current?.Source.StartActivity(
                        $"ProcessEvent.{eventType}");
                        
                    activity?.SetTag("event.type", eventType);
                    activity?.SetTag("event.id", @event.Id);
                    
                    await handler(@event);
                    success = true;
                }
                catch (Exception)
                {
                    success = false;
                    throw;
                }
                finally
                {
                    stopwatch.Stop();
                    
                    _metrics.RecordEventProcessingDuration(
                        eventType, 
                        stopwatch.ElapsedMilliseconds);
                        
                    if (success)
                    {
                        _metrics.IncrementEventProcessedCounter(eventType);
                    }
                    else
                    {
                        _metrics.IncrementEventProcessingFailureCounter(eventType);
                    }
                }
            },
            cancellationToken);
    }
    
    // Implement other methods with similar monitoring
}
```

## Best Practices

1. **Use Asynchronous Event Handlers**: Prefer async event handlers to avoid blocking threads during I/O operations.
2. **Implement Idempotent Handlers**: Ensure event handlers are idempotent to handle duplicate events safely.
3. **Handle Failures Gracefully**: Implement proper error handling and retry logic for event processing failures.
4. **Monitor Subscription Health**: Track metrics like event processing latency, success rates, and queue depths.
5. **Support Catch-Up Subscriptions**: Ensure new or restarted subscribers can catch up on missed events.
6. **Use Dead Letter Queues**: Store failed events in a dead letter queue for later inspection or retry.
7. **Implement Circuit Breakers**: Use circuit breakers to prevent cascading failures when downstream services are unavailable.
8. **Maintain Subscription State**: Store subscription position to resume processing from the last known position after restarts.
9. **Scale Horizontally**: Design subscription systems to scale horizontally for high-throughput scenarios.
10. **Implement Backpressure**: Handle scenarios where events are produced faster than they can be processed.

## Common Pitfalls

1. **Ignoring Duplicate Events**: Failing to handle duplicate events can lead to inconsistent read models.
2. **Synchronous Processing**: Processing events synchronously can lead to thread pool starvation and reduced throughput.
3. **Missing Error Handling**: Inadequate error handling can cause subscription failures and data inconsistencies.
4. **Tight Coupling**: Coupling event handlers directly to specific event store implementations makes it difficult to change storage technologies.
5. **Ignoring Ordering**: In some cases, event order matters; failing to handle this can lead to inconsistent state.
6. **Resource Leaks**: Not properly managing subscriptions can lead to resource leaks and memory issues.
7. **Inadequate Monitoring**: Without proper monitoring, it's difficult to detect when subscriptions are falling behind or failing.

## Related Components

- [Event](./event.md): Base class for domain events processed by subscriptions
- [IEventHandler](./ievent-handler.md): Interface for event handlers registered with subscriptions
- [ReadModelBase](./read-model-base.md): Base class for read models updated by event handlers
- [IReadModelRepository](./iread-model-repository.md): Interface for repositories that store read models

---

**Navigation**:
- [← Previous: Query Handling](./query-handling.md)
- [↑ Back to Top](#event-subscription)
- [→ Next: AggregateRoot](./aggregate-root.md)
